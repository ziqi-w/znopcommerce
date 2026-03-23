using System.Globalization;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Api.Responses;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Handles defensive refund execution for Latipay payments.
/// </summary>
public class LatipayRefundService : ILatipayRefundService
{
    private static readonly TimeSpan RefundLockExpiration = TimeSpan.FromMinutes(2);

    private readonly ILatipayApiClient _latipayApiClient;
    private readonly ILogger _logger;
    private readonly ILocker _locker;
    private readonly ILatipayOrderNoteService _latipayOrderNoteService;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipayRefundRecordService _latipayRefundRecordService;
    private readonly ILatipayRequestFactory _latipayRequestFactory;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly IOrderService _orderService;
    private readonly LatipaySettings _settings;

    public LatipayRefundService(ILatipayApiClient latipayApiClient,
        ILogger logger,
        ILocker locker,
        ILatipayOrderNoteService latipayOrderNoteService,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipayRefundRecordService latipayRefundRecordService,
        ILatipayRequestFactory latipayRequestFactory,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        IOrderService orderService,
        LatipaySettings settings)
    {
        _latipayApiClient = latipayApiClient;
        _logger = logger;
        _locker = locker;
        _latipayOrderNoteService = latipayOrderNoteService;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipayRefundRecordService = latipayRefundRecordService;
        _latipayRequestFactory = latipayRequestFactory;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _orderService = orderService;
        _settings = settings;
    }

    public async Task<LatipayRefundEligibilityResult> EvaluateEligibilityAsync(Order order,
        decimal refundAmount,
        bool isPartialRefund,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(order.PaymentMethodSystemName, LatipayDefaults.SystemName, StringComparison.Ordinal))
        {
            return Denied("This order was not paid with Latipay.");
        }

        if (order.Deleted)
            return Denied("This order is no longer available for refund.");

        if (!_settings.EnableRefunds)
            return Denied("Latipay refunds are disabled in plugin settings.");

        if (refundAmount <= decimal.Zero)
            return Denied("Refund amount must be greater than zero.");

        if (order.OrderTotal <= decimal.Zero)
            return Denied("This order does not have a refundable total.");

        if (isPartialRefund)
        {
            if (!_settings.EnablePartialRefunds)
                return Denied("Partial refunds are disabled for Latipay.");

            if (order.PaymentStatus is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
                return Denied("This order is not in a partially refundable payment state.");
        }
        else if (order.PaymentStatus != PaymentStatus.Paid)
        {
            return Denied("Only paid Latipay orders can be fully refunded.");
        }

        var paymentAttempt = await GetRefundablePaymentAttemptAsync(order, cancellationToken);
        if (paymentAttempt is null)
        {
            return Denied("The confirmed Latipay payment attempt could not be found for this order.");
        }

        var latipayOrderId = ResolveLatipayOrderId(order, paymentAttempt);
        if (string.IsNullOrWhiteSpace(latipayOrderId))
        {
            return Denied("The Latipay order identifier required for refund is missing. Manual review is required before retrying.");
        }

        var successfulAmount = await _latipayRefundRecordService.GetTotalRefundAmountAsync(
            order.Id,
            nameof(LatipayRefundStatus.Succeeded));
        var reservedAmount = await _latipayRefundRecordService.GetTotalRefundAmountAsync(
            order.Id,
            nameof(LatipayRefundStatus.PendingSubmission),
            nameof(LatipayRefundStatus.ReviewRequired));

        var alreadyRefundedAmount = Math.Max(order.RefundedAmount, successfulAmount);
        var remainingRefundableAmount = order.OrderTotal - alreadyRefundedAmount - reservedAmount;
        if (remainingRefundableAmount <= decimal.Zero)
        {
            return Denied("No refundable balance remains after accounting for completed and review-required Latipay refunds.");
        }

        if (refundAmount > remainingRefundableAmount)
        {
            return Denied($"Refund amount exceeds the remaining refundable balance of {remainingRefundableAmount.ToString("0.00", CultureInfo.InvariantCulture)}.");
        }

        return new LatipayRefundEligibilityResult
        {
            CanRefund = true,
            PaymentAttemptId = paymentAttempt.Id,
            LatipayOrderId = latipayOrderId,
            RemainingRefundableAmount = remainingRefundableAmount,
            Message = "The refund request is eligible to be submitted."
        };
    }

    public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refundPaymentRequest);
        ArgumentNullException.ThrowIfNull(refundPaymentRequest.Order);

        RefundPaymentResult result = null;
        var orderId = refundPaymentRequest.Order.Id;
        var lockAcquired = await _locker.PerformActionWithLockAsync(BuildRefundLockKey(orderId), RefundLockExpiration, async () =>
        {
            result = await RefundInternalAsync(refundPaymentRequest, cancellationToken);
        });

        if (!lockAcquired)
        {
            await _logger.WarningAsync($"Latipay refund request was skipped for order #{orderId} because another refund is already in progress.");

            return ErrorResult("A Latipay refund is already being processed for this order. Refresh the order page before trying again.");
        }

        return result ?? ErrorResult("The Latipay refund request did not produce a result.");
    }

    private async Task<RefundPaymentResult> RefundInternalAsync(RefundPaymentRequest refundPaymentRequest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = await _orderService.GetOrderByIdAsync(refundPaymentRequest.Order.Id);
        if (order is null || order.Deleted)
            return ErrorResult("The order could not be loaded for Latipay refund.");

        var eligibility = await EvaluateEligibilityAsync(order,
            refundPaymentRequest.AmountToRefund,
            refundPaymentRequest.IsPartialRefund,
            cancellationToken);

        if (!eligibility.CanRefund)
        {
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                $"Latipay refund was blocked for order #{order.CustomOrderNumber ?? order.Id.ToString()} because the refund was not in a safe state. Reason: {eligibility.Message}");
            return ErrorResult(eligibility.Message);
        }

        var refundReference = await GenerateRefundReferenceAsync(order.Id);
        var refundRecord = new LatipayRefundRecord
        {
            OrderId = order.Id,
            PaymentAttemptId = eligibility.PaymentAttemptId!.Value,
            LatipayOrderId = eligibility.LatipayOrderId,
            RefundReference = refundReference,
            RefundAmount = refundPaymentRequest.AmountToRefund,
            RefundStatus = nameof(LatipayRefundStatus.PendingSubmission),
            ExternalResponseSummary = "Refund request reserved locally before submission."
        };
        await _latipayRefundRecordService.InsertAsync(refundRecord);
        var paymentAttempt = await _latipayPaymentAttemptService.GetByIdAsync(refundRecord.PaymentAttemptId);
        var isHostedCardAttempt = paymentAttempt is not null
            && _latipaySubPaymentMethodService.TryGetMethod(paymentAttempt.SelectedSubPaymentMethod, out var selectedMethod)
            && selectedMethod.IntegrationMode == LatipayIntegrationMode.HostedCard;

        try
        {
            if (isHostedCardAttempt)
            {
                var cardRefundRequest = await _latipayRequestFactory.BuildCardRefundRequestAsync(new CardRefundRequestParameters
                {
                    LatipayOrderId = eligibility.LatipayOrderId,
                    RefundAmount = refundPaymentRequest.AmountToRefund,
                    Reason = refundReference
                }, cancellationToken);

                var cardRefundResponse = await _latipayApiClient.RefundCardTransactionAsync(cardRefundRequest, cancellationToken);
                refundRecord.RefundStatus = nameof(LatipayRefundStatus.Succeeded);
                refundRecord.CompletedOnUtc = DateTime.UtcNow;
                refundRecord.ExternalResponseSummary = BuildSuccessSummary(cardRefundResponse.Code, cardRefundResponse.Message);
            }
            else
            {
                var refundRequest = await _latipayRequestFactory.BuildRefundRequestAsync(new RefundRequestParameters
                {
                    OrderId = eligibility.LatipayOrderId,
                    RefundAmount = refundPaymentRequest.AmountToRefund,
                    Reference = refundReference
                }, cancellationToken);

                var refundResponse = await _latipayApiClient.RefundAsync(refundRequest, cancellationToken);
                refundRecord.RefundStatus = nameof(LatipayRefundStatus.Succeeded);
                refundRecord.CompletedOnUtc = DateTime.UtcNow;
                refundRecord.ExternalResponseSummary = BuildSuccessSummary(refundResponse.Code, refundResponse.Message);
            }

            await _latipayRefundRecordService.UpdateAsync(refundRecord);

            await _latipayOrderNoteService.AddNoteAsync(order,
                BuildSuccessNote(refundPaymentRequest, refundRecord));
            await _logger.InformationAsync(
                $"Latipay refund '{refundReference}' succeeded for order #{order.Id}. Amount: {refundPaymentRequest.AmountToRefund:0.00}. Partial: {refundPaymentRequest.IsPartialRefund}.");

            return new RefundPaymentResult
            {
                NewPaymentStatus = refundPaymentRequest.IsPartialRefund
                    ? PaymentStatus.PartiallyRefunded
                    : PaymentStatus.Refunded
            };
        }
        catch (LatipayApiException exception)
        {
            var isAmbiguous = IsAmbiguousRefundFailure(exception);
            refundRecord.RefundStatus = isAmbiguous
                ? nameof(LatipayRefundStatus.ReviewRequired)
                : nameof(LatipayRefundStatus.Failed);
            refundRecord.CompletedOnUtc = isAmbiguous ? null : DateTime.UtcNow;
            refundRecord.ExternalResponseSummary = BuildFailureSummary(exception);
            await _latipayRefundRecordService.UpdateAsync(refundRecord);

            if (isAmbiguous)
            {
                await _latipayOrderNoteService.AddNoteAsync(order,
                    $"Latipay refund '{refundRecord.RefundReference}' for amount {refundRecord.RefundAmount:0.00} could not be confirmed automatically. The refund is now blocked for manual review before any further refunds are attempted.");
                await _logger.WarningAsync(
                    $"Latipay refund '{refundRecord.RefundReference}' for order #{order.Id} is in manual review. Failure kind: {exception.FailureKind}.",
                    exception);

                return ErrorResult("The Latipay refund result could not be confirmed automatically. The refund has been marked for manual review and no further refund attempts should be made until it is checked.");
            }

            await _latipayOrderNoteService.AddNoteAsync(order,
                $"Latipay refund '{refundRecord.RefundReference}' for amount {refundRecord.RefundAmount:0.00} failed. Reason: {exception.Message}");
            await _logger.WarningAsync(
                $"Latipay refund '{refundRecord.RefundReference}' failed for order #{order.Id}. Failure kind: {exception.FailureKind}.",
                exception);

            return ErrorResult(BuildVisibleFailureMessage(exception));
        }
        catch (Exception exception)
        {
            refundRecord.RefundStatus = nameof(LatipayRefundStatus.ReviewRequired);
            refundRecord.CompletedOnUtc = null;
            refundRecord.ExternalResponseSummary = "Unexpected refund error. Manual review required.";
            await _latipayRefundRecordService.UpdateAsync(refundRecord);

            await _latipayOrderNoteService.AddNoteAsync(order,
                $"Latipay refund '{refundRecord.RefundReference}' hit an unexpected error and now requires manual review before any further refund attempts are made.");
            await _logger.ErrorAsync(
                $"Latipay refund '{refundRecord.RefundReference}' hit an unexpected error for order #{order.Id}.",
                exception);

            return ErrorResult("An unexpected error occurred while submitting the Latipay refund. The refund has been marked for manual review.");
        }
    }

    private async Task<LatipayPaymentAttempt> GetRefundablePaymentAttemptAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        cancellationToken.ThrowIfCancellationRequested();

        var attempts = await _latipayPaymentAttemptService.GetByOrderIdAsync(order.Id);
        var successfulAttempts = attempts
            .Where(attempt => attempt.PaymentCompletedOnUtc.HasValue)
            .OrderByDescending(attempt => attempt.PaymentCompletedOnUtc)
            .ThenByDescending(attempt => attempt.AttemptNumber)
            .ThenByDescending(attempt => attempt.Id)
            .ToList();

        if (!successfulAttempts.Any())
            return null;

        if (!string.IsNullOrWhiteSpace(order.CaptureTransactionId))
        {
            var matchingAttempt = successfulAttempts.FirstOrDefault(attempt =>
                string.Equals(attempt.LatipayOrderId, order.CaptureTransactionId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(attempt.MerchantReference, order.CaptureTransactionId, StringComparison.OrdinalIgnoreCase));
            if (matchingAttempt is not null)
                return matchingAttempt;
        }

        return successfulAttempts.FirstOrDefault();
    }

    private static string ResolveLatipayOrderId(Order order, LatipayPaymentAttempt paymentAttempt)
    {
        if (!string.IsNullOrWhiteSpace(paymentAttempt.LatipayOrderId))
            return paymentAttempt.LatipayOrderId.Trim();

        if (!string.IsNullOrWhiteSpace(order.CaptureTransactionId)
            && !string.Equals(order.CaptureTransactionId, paymentAttempt.MerchantReference, StringComparison.OrdinalIgnoreCase))
        {
            return order.CaptureTransactionId.Trim();
        }

        return null;
    }

    private async Task<string> GenerateRefundReferenceAsync(int orderId)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"ltr-{orderId}-{Guid.NewGuid():N}";
            var reference = candidate[..Math.Min(45, candidate.Length)];
            if (await _latipayRefundRecordService.GetByRefundReferenceAsync(reference) is null)
                return reference;
        }

        throw new InvalidOperationException("A unique Latipay refund reference could not be generated.");
    }

    private static string BuildRefundLockKey(int orderId)
    {
        return $"latipay-refund:{orderId}";
    }

    private static bool IsAmbiguousRefundFailure(LatipayApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.FailureKind is LatipayApiFailureKind.Timeout
            or LatipayApiFailureKind.Transport
            or LatipayApiFailureKind.HttpStatus
            or LatipayApiFailureKind.ResponseValidation
            or LatipayApiFailureKind.Unknown;
    }

    private static string BuildSuccessSummary(string code, string message) =>
        $"Refund accepted by Latipay. Code={NormalizeOptional(code) ?? "0"}; Message={NormalizeOptional(message) ?? "<empty>"}";

    private static string BuildFailureSummary(LatipayApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var providerCodeSegment = string.IsNullOrWhiteSpace(exception.ProviderCode)
            ? string.Empty
            : $" ProviderCode={exception.ProviderCode};";
        return $"Refund failure kind={exception.FailureKind};{providerCodeSegment} Message={NormalizeOptional(exception.Message) ?? "<empty>"}";
    }

    private static string BuildSuccessNote(RefundPaymentRequest refundPaymentRequest, LatipayRefundRecord refundRecord)
    {
        var refundType = refundPaymentRequest.IsPartialRefund ? "partial" : "full";
        return $"Latipay {refundType} refund succeeded for reference '{refundRecord.RefundReference}'. Amount = {refundRecord.RefundAmount:0.00}. Latipay order ID = '{refundRecord.LatipayOrderId}'.";
    }

    private static string BuildVisibleFailureMessage(LatipayApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.FailureKind switch
        {
            LatipayApiFailureKind.Configuration =>
                "Latipay refund configuration is incomplete. Check the plugin settings before trying again.",
            LatipayApiFailureKind.RequestValidation =>
                "The refund request was not valid for Latipay. Review the order and refund amount before trying again.",
            LatipayApiFailureKind.Provider =>
                $"Latipay rejected the refund request: {NormalizeOptional(exception.Message) ?? "Unknown provider error."}",
            _ =>
                "The Latipay refund could not be completed."
        };
    }

    private static RefundPaymentResult ErrorResult(string error)
    {
        var result = new RefundPaymentResult();
        result.AddError(error);
        return result;
    }

    private static LatipayRefundEligibilityResult Denied(string message)
    {
        return new LatipayRefundEligibilityResult
        {
            CanRefund = false,
            Message = message
        };
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
