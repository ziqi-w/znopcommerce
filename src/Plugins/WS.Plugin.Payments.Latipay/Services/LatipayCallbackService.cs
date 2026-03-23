using System.Security.Cryptography;
using System.Text;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Handles idempotent, concurrency-safe Latipay callback processing.
/// </summary>
public class LatipayCallbackService : ILatipayCallbackService
{
    private static readonly TimeSpan AttemptLockExpiration = TimeSpan.FromMinutes(2);

    private readonly ILogger _logger;
    private readonly ILocker _locker;
    private readonly ILatipayOrderNoteService _latipayOrderNoteService;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipaySignatureService _latipaySignatureService;
    private readonly ILatipayStateMachine _latipayStateMachine;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly ILatipayTransactionStatusMapper _latipayTransactionStatusMapper;
    private readonly IOrderService _orderService;
    private readonly LatipaySettings _settings;

    public LatipayCallbackService(ILogger logger,
        ILocker locker,
        ILatipayOrderNoteService latipayOrderNoteService,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipaySignatureService latipaySignatureService,
        ILatipayStateMachine latipayStateMachine,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        ILatipayTransactionStatusMapper latipayTransactionStatusMapper,
        IOrderService orderService,
        LatipaySettings settings)
    {
        _logger = logger;
        _locker = locker;
        _latipayOrderNoteService = latipayOrderNoteService;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipaySignatureService = latipaySignatureService;
        _latipayStateMachine = latipayStateMachine;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _latipayTransactionStatusMapper = latipayTransactionStatusMapper;
        _orderService = orderService;
        _settings = settings;
    }

    public async Task<LatipayCallbackProcessResult> ProcessCallbackAsync(LatipayStatusNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var merchantReference = NormalizeOptional(notification.MerchantReference);
        if (string.IsNullOrWhiteSpace(merchantReference))
        {
            await _logger.WarningAsync("Latipay callback was received without a merchant reference.");
            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = true,
                Message = "The callback did not contain a merchant reference."
            };
        }

        LatipayCallbackProcessResult result = null;
        var lockAcquired = await _locker.PerformActionWithLockAsync(BuildAttemptLockKey(merchantReference), AttemptLockExpiration, async () =>
        {
            result = await ProcessInternalAsync(notification, cancellationToken);
        });

        if (!lockAcquired)
        {
            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = false,
                MerchantReference = merchantReference,
                Message = $"A callback is already being processed for merchant reference '{merchantReference}'."
            };
        }

        return result ?? new LatipayCallbackProcessResult
        {
            AcknowledgeCallback = false,
            MerchantReference = merchantReference,
            Message = $"The callback for merchant reference '{merchantReference}' did not produce a result."
        };
    }

    private async Task<LatipayCallbackProcessResult> ProcessInternalAsync(LatipayStatusNotification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var merchantReference = NormalizeOptional(notification.MerchantReference);
        var attempt = await _latipayPaymentAttemptService.GetByMerchantReferenceAsync(merchantReference);
        if (attempt is null)
        {
            await _logger.WarningAsync($"Latipay callback was received for unknown merchant reference '{merchantReference}'.");
            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = true,
                MerchantReference = merchantReference,
                Message = "No payment attempt matched the callback merchant reference."
            };
        }

        var order = await _orderService.GetOrderByIdAsync(attempt.OrderId);
        if (order is null || order.Deleted)
        {
            attempt.FailureReasonSummary = $"Latipay callback could not load nopCommerce order #{attempt.OrderId} for merchant reference '{merchantReference}'.";
            await _latipayPaymentAttemptService.UpdateAsync(attempt);

            await _logger.WarningAsync(
                $"Latipay callback was received for merchant reference '{merchantReference}', but nopCommerce order #{attempt.OrderId} could not be loaded.");

            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = true,
                MerchantReference = merchantReference,
                Message = attempt.FailureReasonSummary
            };
        }

        if (!HasRequiredVerificationFields(attempt, notification))
        {
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                $"Latipay callback for merchant reference '{merchantReference}' was missing required verification fields. The order remains pending.");
            await _logger.WarningAsync(
                $"Latipay callback for merchant reference '{merchantReference}' was missing required verification fields.");

            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = true,
                MerchantReference = merchantReference,
                Message = "The callback was missing required verification fields."
            };
        }

        var callbackIdempotencyKey = ComputeIdempotencyKey(notification);
        if (IsProcessedDuplicate(attempt, order, notification, callbackIdempotencyKey))
        {
            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = true,
                IsDuplicate = true,
                MerchantReference = merchantReference,
                Message = "The callback had already been processed."
            };
        }

        var signatureValid = IsHostedCardAttempt(attempt)
            ? IsCardCallbackSignatureValid(notification)
            : !string.IsNullOrWhiteSpace(_settings.ApiKey)
                && _latipaySignatureService.IsStatusSignatureValid(
                    notification.MerchantReference,
                    notification.PaymentMethod,
                    notification.Status,
                    notification.Currency,
                    notification.Amount,
                    notification.Signature,
                    _settings.ApiKey);

        if (!signatureValid)
        {
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                $"Latipay callback for merchant reference '{merchantReference}' failed signature validation. The order remains pending.");
            await _logger.WarningAsync(
                $"Latipay callback signature validation failed for merchant reference '{merchantReference}'.");

            return new LatipayCallbackProcessResult
            {
                AcknowledgeCallback = true,
                MerchantReference = merchantReference,
                Message = "The callback signature could not be verified."
            };
        }

        attempt.CallbackReceivedOnUtc = DateTime.UtcNow;
        attempt.CallbackVerified = true;
        attempt.CallbackIdempotencyKey = callbackIdempotencyKey;

        var transitionResult = await _latipayStateMachine.ApplyVerifiedStatusAsync(
            order,
            attempt,
            notification,
            "callback",
            cancellationToken);

        return new LatipayCallbackProcessResult
        {
            AcknowledgeCallback = true,
            MerchantReference = merchantReference,
            Message = transitionResult.Message
        };
    }

    private bool IsProcessedDuplicate(LatipayPaymentAttempt paymentAttempt,
        Order order,
        LatipayStatusNotification notification,
        string callbackIdempotencyKey)
    {
        if (paymentAttempt is null || order is null || string.IsNullOrWhiteSpace(callbackIdempotencyKey))
            return false;

        if (!string.Equals(paymentAttempt.CallbackIdempotencyKey, callbackIdempotencyKey, StringComparison.Ordinal))
            return false;

        var normalizedStatus = _latipayTransactionStatusMapper.Normalize(notification.Status);
        if (normalizedStatus == LatipayTransactionStatus.Paid)
        {
            var attemptTransactionId = ResolveTransactionId(paymentAttempt, notification);
            var transactionAlreadyRecorded = string.IsNullOrWhiteSpace(notification.OrderId)
                || (!string.IsNullOrWhiteSpace(paymentAttempt.LatipayOrderId)
                    && string.Equals(paymentAttempt.LatipayOrderId, attemptTransactionId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(order.CaptureTransactionId)
                    && string.Equals(order.CaptureTransactionId, attemptTransactionId, StringComparison.OrdinalIgnoreCase));

            return order.PaymentStatus == PaymentStatus.Paid
                && paymentAttempt.PaymentCompletedOnUtc.HasValue
                && transactionAlreadyRecorded;
        }

        return paymentAttempt.CallbackVerified
            && string.Equals(NormalizeOptional(paymentAttempt.ExternalStatus), NormalizeOptional(notification.Status), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAttemptLockKey(string merchantReference)
    {
        return $"latipay-attempt:{merchantReference}";
    }

    private bool HasRequiredVerificationFields(LatipayPaymentAttempt paymentAttempt, LatipayStatusNotification notification)
    {
        ArgumentNullException.ThrowIfNull(paymentAttempt);
        ArgumentNullException.ThrowIfNull(notification);

        return IsHostedCardAttempt(paymentAttempt)
            ? notification.HasCardCallbackSignatureFields
            : notification.HasSignatureFields;
    }

    private bool IsHostedCardAttempt(LatipayPaymentAttempt paymentAttempt)
    {
        ArgumentNullException.ThrowIfNull(paymentAttempt);

        return _latipaySubPaymentMethodService.TryGetMethod(paymentAttempt.SelectedSubPaymentMethod, out var method)
            && method.IntegrationMode == Domain.Enums.LatipayIntegrationMode.HostedCard;
    }

    private bool IsCardCallbackSignatureValid(LatipayStatusNotification notification)
    {
        if (string.IsNullOrWhiteSpace(_settings.CardPrivateKey) || !notification.HasCardCallbackSignatureFields)
            return false;

        return _latipaySignatureService.IsSortedParameterSignatureValid(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = notification.Amount,
                ["currency"] = notification.Currency,
                ["notify_version"] = notification.NotifyVersion,
                ["order_id"] = notification.OrderId,
                ["out_trade_no"] = notification.MerchantReference,
                ["pay_time"] = notification.PayTime,
                ["payment_method"] = notification.PaymentMethod,
                ["status"] = notification.Status
            },
            notification.Signature,
            _settings.CardPrivateKey,
            urlEncodeValues: true);
    }

    private static string ComputeIdempotencyKey(LatipayStatusNotification notification)
    {
        var payload = string.Join("|", new[]
        {
            NormalizeOptional(notification.MerchantReference),
            NormalizeOptional(notification.PaymentMethod),
            NormalizeOptional(notification.Status),
            NormalizeOptional(notification.Currency),
            NormalizeOptional(notification.Amount),
            NormalizeOptional(notification.Signature)
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveTransactionId(LatipayPaymentAttempt paymentAttempt, LatipayStatusNotification notification)
    {
        return !string.IsNullOrWhiteSpace(notification.OrderId)
            ? notification.OrderId.Trim()
            : !string.IsNullOrWhiteSpace(paymentAttempt.LatipayOrderId)
                ? paymentAttempt.LatipayOrderId
                : paymentAttempt.MerchantReference;
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
