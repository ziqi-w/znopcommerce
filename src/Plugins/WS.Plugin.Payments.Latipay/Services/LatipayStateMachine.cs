using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Orders;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Applies verified Latipay status transitions to plugin attempts and nopCommerce orders.
/// </summary>
public class LatipayStateMachine : ILatipayStateMachine
{
    private readonly ILatipayOrderNoteService _latipayOrderNoteService;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly ILatipayTransactionStatusMapper _latipayTransactionStatusMapper;
    private readonly IOrderProcessingService _orderProcessingService;

    public LatipayStateMachine(ILatipayOrderNoteService latipayOrderNoteService,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        ILatipayTransactionStatusMapper latipayTransactionStatusMapper,
        IOrderProcessingService orderProcessingService)
    {
        _latipayOrderNoteService = latipayOrderNoteService;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _latipayTransactionStatusMapper = latipayTransactionStatusMapper;
        _orderProcessingService = orderProcessingService;
    }

    public async Task<LatipayStateTransitionResult> ApplyVerifiedStatusAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipayStatusNotification notification,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(paymentAttempt);
        ArgumentNullException.ThrowIfNull(notification);

        cancellationToken.ThrowIfCancellationRequested();

        var sourceLabel = NormalizeSource(source);
        var normalizedStatus = _latipayTransactionStatusMapper.Normalize(notification.Status);
        var previousStatus = _latipayTransactionStatusMapper.Normalize(paymentAttempt.ExternalStatus);
        var resolvedMethod = _latipaySubPaymentMethodService.TryGetMethod(paymentAttempt.SelectedSubPaymentMethod, out var selectedMethod)
            ? selectedMethod
            : null;

        paymentAttempt.ExternalStatus = NormalizeOptional(notification.Status);
        if (!string.IsNullOrWhiteSpace(notification.OrderId)
            && !(resolvedMethod?.IntegrationMode == Domain.Enums.LatipayIntegrationMode.HostedCard
                && string.Equals(notification.OrderId.Trim(), paymentAttempt.MerchantReference, StringComparison.OrdinalIgnoreCase)))
        {
            paymentAttempt.LatipayOrderId = notification.OrderId.Trim();
        }

        var validationMessage = ValidateVerifiedNotification(paymentAttempt, notification);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            paymentAttempt.FailureReasonSummary = validationMessage;
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                BuildNote(validationMessage, sourceLabel, paymentAttempt.MerchantReference));

            return new LatipayStateTransitionResult
            {
                Changed = true,
                KeepPending = true,
                ReviewRequired = true,
                AppliedStatus = NormalizeOptional(notification.Status),
                Message = validationMessage
            };
        }

        var illegalTransitionMessage = GetIllegalTransitionMessage(previousStatus, normalizedStatus, order, paymentAttempt);
        if (!string.IsNullOrWhiteSpace(illegalTransitionMessage))
        {
            paymentAttempt.FailureReasonSummary = illegalTransitionMessage;
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                BuildNote(illegalTransitionMessage, sourceLabel, paymentAttempt.MerchantReference));

            return new LatipayStateTransitionResult
            {
                Changed = true,
                KeepPending = true,
                ReviewRequired = true,
                AppliedStatus = NormalizeOptional(notification.Status),
                Message = illegalTransitionMessage
            };
        }

        return normalizedStatus switch
        {
            LatipayTransactionStatus.Paid => await HandlePaidStatusAsync(order, paymentAttempt, notification, sourceLabel),
            LatipayTransactionStatus.Failed or LatipayTransactionStatus.Canceled or LatipayTransactionStatus.Rejected
                => await HandleTerminalNonPaidStatusAsync(order, paymentAttempt, notification, normalizedStatus, sourceLabel),
            LatipayTransactionStatus.Pending
                => await HandlePendingStatusAsync(order, paymentAttempt, notification, sourceLabel),
            _
                => await HandleUnknownStatusAsync(order, paymentAttempt, notification, sourceLabel)
        };
    }

    private async Task<LatipayStateTransitionResult> HandlePaidStatusAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipayStatusNotification notification,
        string sourceLabel)
    {
        var attemptTransactionId = ResolveTransactionId(paymentAttempt, notification);
        var alreadyApplied = order.PaymentStatus == PaymentStatus.Paid
            && (paymentAttempt.PaymentCompletedOnUtc.HasValue
                || (!string.IsNullOrWhiteSpace(order.CaptureTransactionId)
                    && string.Equals(order.CaptureTransactionId, attemptTransactionId, StringComparison.OrdinalIgnoreCase)));

        if (alreadyApplied)
        {
            paymentAttempt.PaymentCompletedOnUtc ??= DateTime.UtcNow;
            paymentAttempt.FailureReasonSummary = null;
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);

            return new LatipayStateTransitionResult
            {
                Changed = true,
                MarkedPaid = true,
                AppliedStatus = NormalizeOptional(notification.Status),
                Message = "The verified paid status was already applied."
            };
        }

        if (!_orderProcessingService.CanMarkOrderAsPaid(order))
        {
            var reviewMessage = $"Latipay {sourceLabel} verified a paid status for merchant reference '{paymentAttempt.MerchantReference}', but the nopCommerce order could not be marked as paid automatically. Manual review is required.";
            paymentAttempt.FailureReasonSummary = reviewMessage;
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                BuildNote(reviewMessage, sourceLabel, paymentAttempt.MerchantReference));

            return new LatipayStateTransitionResult
            {
                Changed = true,
                KeepPending = true,
                ReviewRequired = true,
                AppliedStatus = NormalizeOptional(notification.Status),
                Message = reviewMessage
            };
        }

        paymentAttempt.PaymentCompletedOnUtc ??= DateTime.UtcNow;
        paymentAttempt.FailureReasonSummary = null;

        order.CaptureTransactionId = attemptTransactionId;
        order.CaptureTransactionResult = NormalizeOptional(notification.Status);

        await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
        await _orderProcessingService.MarkOrderAsPaidAsync(order);
        await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
            $"Latipay {sourceLabel} verified paid status '{NormalizeOptional(notification.Status) ?? "paid"}' for merchant reference '{paymentAttempt.MerchantReference}'.");

        return new LatipayStateTransitionResult
        {
            Changed = true,
            MarkedPaid = true,
            AppliedStatus = NormalizeOptional(notification.Status),
            Message = "The order was marked as paid from a verified Latipay status."
        };
    }

    private async Task<LatipayStateTransitionResult> HandleTerminalNonPaidStatusAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipayStatusNotification notification,
        LatipayTransactionStatus normalizedStatus,
        string sourceLabel)
    {
        var message = $"Latipay {sourceLabel} verified status '{NormalizeOptional(notification.Status) ?? normalizedStatus.ToString().ToLowerInvariant()}' for merchant reference '{paymentAttempt.MerchantReference}'. The order remains pending for retry or review.";
        paymentAttempt.FailureReasonSummary = message;
        await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
        await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
            BuildNote(message, sourceLabel, paymentAttempt.MerchantReference));

        return new LatipayStateTransitionResult
        {
            Changed = true,
            KeepPending = true,
            AppliedStatus = NormalizeOptional(notification.Status),
            Message = message
        };
    }

    private async Task<LatipayStateTransitionResult> HandlePendingStatusAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipayStatusNotification notification,
        string sourceLabel)
    {
        var message = $"Latipay {sourceLabel} verified pending status for merchant reference '{paymentAttempt.MerchantReference}'. The order remains pending while confirmation continues.";
        paymentAttempt.FailureReasonSummary = message;
        await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
        await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
            BuildNote(message, sourceLabel, paymentAttempt.MerchantReference));

        return new LatipayStateTransitionResult
        {
            Changed = true,
            KeepPending = true,
            AppliedStatus = NormalizeOptional(notification.Status),
            Message = message
        };
    }

    private async Task<LatipayStateTransitionResult> HandleUnknownStatusAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipayStatusNotification notification,
        string sourceLabel)
    {
        var message = $"Latipay {sourceLabel} returned unrecognized status '{NormalizeOptional(notification.Status) ?? "<empty>"}' for merchant reference '{paymentAttempt.MerchantReference}'. The order remains pending for review.";
        paymentAttempt.FailureReasonSummary = message;
        await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
        await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
            BuildNote(message, sourceLabel, paymentAttempt.MerchantReference));

        return new LatipayStateTransitionResult
        {
            Changed = true,
            KeepPending = true,
            ReviewRequired = true,
            AppliedStatus = NormalizeOptional(notification.Status),
            Message = message
        };
    }

    private string ValidateVerifiedNotification(LatipayPaymentAttempt paymentAttempt, LatipayStatusNotification notification)
    {
        if (!string.Equals(paymentAttempt.MerchantReference, notification.MerchantReference?.Trim(), StringComparison.Ordinal))
        {
            return $"Latipay returned merchant reference '{notification.MerchantReference}' which does not match payment attempt '{paymentAttempt.MerchantReference}'. The order remains pending.";
        }

        if (!notification.TryGetAmountValue(out var amount) || amount != paymentAttempt.Amount)
        {
            return $"Latipay returned amount '{notification.Amount}' for merchant reference '{paymentAttempt.MerchantReference}', but the original attempt amount was '{paymentAttempt.Amount:0.00}'. The order remains pending.";
        }

        if (!string.Equals(notification.Currency?.Trim(), LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(paymentAttempt.Currency, notification.Currency?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return $"Latipay returned currency '{notification.Currency}' for merchant reference '{paymentAttempt.MerchantReference}', but this plugin only accepts '{paymentAttempt.Currency}'. The order remains pending.";
        }

        if (!_latipaySubPaymentMethodService.TryGetMethodByProviderValue(notification.PaymentMethod, out var method))
        {
            return $"Latipay returned an unknown payment method '{notification.PaymentMethod}' for merchant reference '{paymentAttempt.MerchantReference}'. The order remains pending.";
        }

        if (!string.IsNullOrWhiteSpace(paymentAttempt.SelectedSubPaymentMethod)
            && !string.Equals(paymentAttempt.SelectedSubPaymentMethod, method.Key, StringComparison.OrdinalIgnoreCase))
        {
            return $"Latipay returned payment method '{notification.PaymentMethod}' for merchant reference '{paymentAttempt.MerchantReference}', but the original attempt used '{paymentAttempt.SelectedSubPaymentMethod}'. The order remains pending.";
        }

        return null;
    }

    private string GetIllegalTransitionMessage(LatipayTransactionStatus previousStatus,
        LatipayTransactionStatus newStatus,
        Order order,
        LatipayPaymentAttempt paymentAttempt)
    {
        if (previousStatus == LatipayTransactionStatus.Unknown)
            return null;

        if (previousStatus == LatipayTransactionStatus.Paid && newStatus != LatipayTransactionStatus.Paid)
        {
            return $"Latipay reported status '{newStatus}' after a paid result had already been applied for merchant reference '{paymentAttempt.MerchantReference}'. Manual review is required.";
        }

        if (IsTerminalNonPaid(previousStatus) && newStatus == LatipayTransactionStatus.Paid)
        {
            return $"Latipay reported a paid result after terminal status '{previousStatus}' was already stored for merchant reference '{paymentAttempt.MerchantReference}'. Manual review is required.";
        }

        if ((previousStatus == LatipayTransactionStatus.Paid || IsTerminalNonPaid(previousStatus))
            && newStatus == LatipayTransactionStatus.Pending)
        {
            return $"Latipay reported a pending status after terminal status '{previousStatus}' was already stored for merchant reference '{paymentAttempt.MerchantReference}'. Manual review is required.";
        }

        return null;
    }

    private static bool IsTerminalNonPaid(LatipayTransactionStatus status)
    {
        return status is LatipayTransactionStatus.Failed or LatipayTransactionStatus.Canceled or LatipayTransactionStatus.Rejected;
    }

    private static string ResolveTransactionId(LatipayPaymentAttempt paymentAttempt, LatipayStatusNotification notification)
    {
        return !string.IsNullOrWhiteSpace(notification.OrderId)
            ? notification.OrderId.Trim()
            : !string.IsNullOrWhiteSpace(paymentAttempt.LatipayOrderId)
                ? paymentAttempt.LatipayOrderId
                : paymentAttempt.MerchantReference;
    }

    private static string BuildNote(string message, string sourceLabel, string merchantReference)
    {
        return string.IsNullOrWhiteSpace(merchantReference)
            ? message
            : $"[{sourceLabel}] {message}";
    }

    private static string NormalizeSource(string source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? "verification"
            : source.Trim();
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
