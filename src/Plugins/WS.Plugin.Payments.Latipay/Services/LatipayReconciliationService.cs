using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Reconciles unresolved payment attempts against Latipay's query endpoint.
/// </summary>
public class LatipayReconciliationService : ILatipayReconciliationService
{
    private static readonly TimeSpan AttemptLockExpiration = TimeSpan.FromMinutes(2);

    private readonly ILatipayApiClient _latipayApiClient;
    private readonly ILogger _logger;
    private readonly ILocker _locker;
    private readonly ILatipayOrderNoteService _latipayOrderNoteService;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipayRequestFactory _latipayRequestFactory;
    private readonly ILatipayStateMachine _latipayStateMachine;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly ILatipayTransactionStatusMapper _latipayTransactionStatusMapper;
    private readonly IOrderService _orderService;
    private readonly LatipaySettings _settings;

    public LatipayReconciliationService(ILatipayApiClient latipayApiClient,
        ILogger logger,
        ILocker locker,
        ILatipayOrderNoteService latipayOrderNoteService,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipayRequestFactory latipayRequestFactory,
        ILatipayStateMachine latipayStateMachine,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        ILatipayTransactionStatusMapper latipayTransactionStatusMapper,
        IOrderService orderService,
        LatipaySettings settings)
    {
        _latipayApiClient = latipayApiClient;
        _logger = logger;
        _locker = locker;
        _latipayOrderNoteService = latipayOrderNoteService;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipayRequestFactory = latipayRequestFactory;
        _latipayStateMachine = latipayStateMachine;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _latipayTransactionStatusMapper = latipayTransactionStatusMapper;
        _orderService = orderService;
        _settings = settings;
    }

    public async Task<LatipayReconciliationResult> ReconcileByMerchantReferenceAsync(string merchantReference, string trigger, CancellationToken cancellationToken = default)
    {
        merchantReference = NormalizeOptional(merchantReference);
        if (string.IsNullOrWhiteSpace(merchantReference))
        {
            return new LatipayReconciliationResult
            {
                KeepPending = true,
                ReviewRequired = true,
                Message = "Latipay reconciliation could not start because the merchant reference is missing."
            };
        }

        LatipayReconciliationResult result = null;
        var lockAcquired = await _locker.PerformActionWithLockAsync(BuildAttemptLockKey(merchantReference), AttemptLockExpiration, async () =>
        {
            result = await ReconcileInternalAsync(merchantReference, NormalizeTrigger(trigger), cancellationToken);
        });

        if (!lockAcquired)
        {
            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                KeepPending = true,
                Message = $"A Latipay reconciliation is already running for merchant reference '{merchantReference}'."
            };
        }

        return result ?? new LatipayReconciliationResult
        {
            MerchantReference = merchantReference,
            KeepPending = true,
            ReviewRequired = true,
            Message = $"Latipay reconciliation for merchant reference '{merchantReference}' did not produce a result."
        };
    }

    public async Task<LatipayReconciliationResult> ReconcileLatestAttemptForOrderAsync(int orderId, string trigger, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return new LatipayReconciliationResult
            {
                KeepPending = true,
                ReviewRequired = true,
                Message = "Latipay reconciliation could not start because the order identifier is invalid."
            };
        }

        var attempt = await _latipayPaymentAttemptService.GetLatestByOrderIdAsync(orderId);
        if (attempt is null)
        {
            return new LatipayReconciliationResult
            {
                KeepPending = true,
                ReviewRequired = true,
                OrderId = orderId,
                Message = $"No Latipay payment attempt was found for order #{orderId}."
            };
        }

        return await ReconcileByMerchantReferenceAsync(attempt.MerchantReference, trigger, cancellationToken);
    }

    public async Task<bool> CanRetryPaymentAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.OrderStatus == OrderStatus.Cancelled || order.Deleted || order.OrderTotal <= decimal.Zero)
            return false;

        if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.Voided)
            return false;

        var latestAttempt = await _latipayPaymentAttemptService.GetLatestByOrderIdAsync(order.Id);
        if (latestAttempt is null)
            return true;

        if (latestAttempt.PaymentCompletedOnUtc.HasValue)
            return false;

        if (!latestAttempt.RedirectCreatedOnUtc.HasValue)
            return true;

        var latestStatus = _latipayTransactionStatusMapper.Normalize(latestAttempt.ExternalStatus);
        if (IsRetrySafeStatus(latestStatus))
            return true;

        if (IsInsideRetryGuard(latestAttempt))
            return false;

        if (!string.IsNullOrWhiteSpace(latestAttempt.MerchantReference))
            await ReconcileByMerchantReferenceAsync(latestAttempt.MerchantReference, "retry eligibility check", cancellationToken);

        var refreshedOrder = await _orderService.GetOrderByIdAsync(order.Id) ?? order;
        if (refreshedOrder.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.Voided)
            return false;

        var refreshedAttempt = await _latipayPaymentAttemptService.GetByIdAsync(latestAttempt.Id) ?? latestAttempt;
        return IsRetrySafeStatus(_latipayTransactionStatusMapper.Normalize(refreshedAttempt.ExternalStatus));
    }

    public async Task ReconcilePendingAttemptsAsync(CancellationToken cancellationToken = default)
    {
        var attempts = await _latipayPaymentAttemptService.GetUnresolvedAttemptsAsync();
        foreach (var attempt in attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt is null || string.IsNullOrWhiteSpace(attempt.MerchantReference))
                continue;

            var normalizedStatus = _latipayTransactionStatusMapper.Normalize(attempt.ExternalStatus);
            if (attempt.PaymentCompletedOnUtc.HasValue || IsRetrySafeStatus(normalizedStatus))
                continue;

            await ReconcileByMerchantReferenceAsync(attempt.MerchantReference, "scheduled reconciliation", cancellationToken);
        }
    }

    private async Task<LatipayReconciliationResult> ReconcileInternalAsync(string merchantReference, string trigger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attempt = await _latipayPaymentAttemptService.GetByMerchantReferenceAsync(merchantReference);
        if (attempt is null)
        {
            await _logger.WarningAsync($"Latipay {trigger} skipped because no payment attempt exists for merchant reference '{merchantReference}'.");
            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                KeepPending = true,
                ReviewRequired = true,
                Message = $"No Latipay payment attempt exists for merchant reference '{merchantReference}'."
            };
        }

        var order = await _orderService.GetOrderByIdAsync(attempt.OrderId);
        if (order is null || order.Deleted)
        {
            attempt.LastQueriedOnUtc = DateTime.UtcNow;
            attempt.FailureReasonSummary = $"Latipay {trigger} could not load nopCommerce order #{attempt.OrderId} for merchant reference '{merchantReference}'.";
            await _latipayPaymentAttemptService.UpdateAsync(attempt);

            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                OrderId = attempt.OrderId,
                KeepPending = true,
                ReviewRequired = true,
                Message = attempt.FailureReasonSummary
            };
        }

        if (order.PaymentStatus == PaymentStatus.Paid && attempt.PaymentCompletedOnUtc.HasValue)
        {
            attempt.LastQueriedOnUtc = DateTime.UtcNow;
            await _latipayPaymentAttemptService.UpdateAsync(attempt);

            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                OrderId = order.Id,
                IsVerified = true,
                IsPaid = true,
                ExternalStatus = attempt.ExternalStatus,
                Message = "The order was already confirmed as paid."
            };
        }

        try
        {
            var statusNotification = await BuildStatusNotificationAsync(attempt, merchantReference, cancellationToken);
            attempt.LastQueriedOnUtc = DateTime.UtcNow;

            var transitionResult = await _latipayStateMachine.ApplyVerifiedStatusAsync(
                order,
                attempt,
                statusNotification,
                trigger,
                cancellationToken);

            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                OrderId = order.Id,
                IsVerified = true,
                IsPaid = transitionResult.MarkedPaid,
                KeepPending = transitionResult.KeepPending,
                ReviewRequired = transitionResult.ReviewRequired,
                ExternalStatus = attempt.ExternalStatus,
                Message = transitionResult.Message
            };
        }
        catch (LatipayApiException exception)
        {
            attempt.LastQueriedOnUtc = DateTime.UtcNow;
            attempt.FailureReasonSummary = $"Latipay {trigger} failed for merchant reference '{merchantReference}': {exception.Message}";
            await _latipayPaymentAttemptService.UpdateAsync(attempt);

            if (IsManualOrCustomerFacingTrigger(trigger))
            {
                await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                    $"Latipay {trigger} could not confirm merchant reference '{merchantReference}'. The order remains pending. Reason: {exception.Message}");
            }

            await _logger.WarningAsync(
                $"Latipay {trigger} failed for merchant reference '{merchantReference}'. Failure kind: {exception.FailureKind}. Transient: {exception.IsTransient}.",
                exception);

            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                OrderId = order.Id,
                KeepPending = true,
                ReviewRequired = exception.FailureKind is LatipayApiFailureKind.ResponseValidation or LatipayApiFailureKind.SignatureValidation,
                ExternalStatus = attempt.ExternalStatus,
                Message = attempt.FailureReasonSummary
            };
        }
        catch (Exception exception)
        {
            attempt.LastQueriedOnUtc = DateTime.UtcNow;
            attempt.FailureReasonSummary = $"Latipay {trigger} hit an unexpected error for merchant reference '{merchantReference}'.";
            await _latipayPaymentAttemptService.UpdateAsync(attempt);

            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                $"Latipay {trigger} hit an unexpected error for merchant reference '{merchantReference}'. The order remains pending for review.");
            await _logger.ErrorAsync(
                $"Latipay {trigger} hit an unexpected error for merchant reference '{merchantReference}'.",
                exception);

            return new LatipayReconciliationResult
            {
                MerchantReference = merchantReference,
                OrderId = order.Id,
                KeepPending = true,
                ReviewRequired = true,
                ExternalStatus = attempt.ExternalStatus,
                Message = attempt.FailureReasonSummary
            };
        }
    }

    private bool IsInsideRetryGuard(LatipayPaymentAttempt paymentAttempt)
    {
        var retryGuardMinutes = _settings.RetryGuardMinutes >= LatipayDefaults.MinRetryGuardMinutes
            ? _settings.RetryGuardMinutes
            : LatipayDefaults.DefaultRetryGuardMinutes;

        var guardStartUtc = DateTime.UtcNow.AddMinutes(-retryGuardMinutes);
        var referenceTime = paymentAttempt.LastQueriedOnUtc
            ?? paymentAttempt.CallbackReceivedOnUtc
            ?? paymentAttempt.RedirectCreatedOnUtc
            ?? paymentAttempt.CreatedOnUtc;

        return referenceTime >= guardStartUtc;
    }

    private static bool IsRetrySafeStatus(LatipayTransactionStatus status)
    {
        return status is LatipayTransactionStatus.Failed or LatipayTransactionStatus.Canceled or LatipayTransactionStatus.Rejected;
    }

    private static bool IsManualOrCustomerFacingTrigger(string trigger)
    {
        return trigger.Contains("manual", StringComparison.OrdinalIgnoreCase)
            || trigger.Contains("return", StringComparison.OrdinalIgnoreCase)
            || trigger.Contains("retry", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LatipayStatusNotification> BuildStatusNotificationAsync(LatipayPaymentAttempt attempt,
        string merchantReference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (_latipaySubPaymentMethodService.TryGetMethod(attempt.SelectedSubPaymentMethod, out var method)
            && method.IntegrationMode == Domain.Enums.LatipayIntegrationMode.HostedCard)
        {
            var cardQueryRequest = await _latipayRequestFactory.BuildCardQueryTransactionRequestAsync(new CardQueryTransactionRequestParameters
            {
                MerchantReference = merchantReference
            }, cancellationToken);

            var cardQueryResponse = await _latipayApiClient.QueryCardTransactionAsync(cardQueryRequest, cancellationToken);
            return LatipayStatusNotification.FromCardQueryResponse(cardQueryResponse, merchantReference);
        }

        var legacyQueryRequest = await _latipayRequestFactory.BuildQueryTransactionRequestAsync(new QueryTransactionRequestParameters
        {
            MerchantReference = merchantReference
        }, cancellationToken);

        var legacyQueryResponse = await _latipayApiClient.QueryTransactionAsync(legacyQueryRequest, cancellationToken);
        return LatipayStatusNotification.FromQueryResponse(legacyQueryResponse);
    }

    private static string BuildAttemptLockKey(string merchantReference)
    {
        return $"latipay-attempt:{merchantReference}";
    }

    private static string NormalizeTrigger(string trigger)
    {
        return string.IsNullOrWhiteSpace(trigger)
            ? "reconciliation"
            : trigger.Trim();
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
