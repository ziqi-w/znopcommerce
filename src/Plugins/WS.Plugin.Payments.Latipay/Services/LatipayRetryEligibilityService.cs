using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Determines whether a Latipay order is currently safe to retry.
/// </summary>
public class LatipayRetryEligibilityService : ILatipayRetryEligibilityService
{
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipayReconciliationService _latipayReconciliationService;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly ILatipayTransactionStatusMapper _latipayTransactionStatusMapper;
    private readonly LatipaySettings _settings;

    public LatipayRetryEligibilityService(ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipayReconciliationService latipayReconciliationService,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        ILatipayTransactionStatusMapper latipayTransactionStatusMapper,
        LatipaySettings settings)
    {
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipayReconciliationService = latipayReconciliationService;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _latipayTransactionStatusMapper = latipayTransactionStatusMapper;
        _settings = settings;
    }

    public async Task<LatipayRetryEligibilityResult> EvaluateAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(order.PaymentMethodSystemName, LatipayDefaults.SystemName, StringComparison.Ordinal))
        {
            return Denied("This order was not placed with Latipay.");
        }

        if (order.Deleted)
            return Denied("This order is no longer available for payment retry.");

        if (order.OrderStatus == OrderStatus.Cancelled)
            return Denied("This order has been cancelled and cannot be paid again.");

        if (order.OrderTotal <= decimal.Zero)
            return Denied("This order no longer has a payable balance.");

        if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.Voided)
            return Denied("This order is no longer payable through Latipay.");

        if (order.PaymentStatus != PaymentStatus.Pending)
            return Denied("This order is not in a retryable payment state.");

        if (!string.Equals(order.CustomerCurrencyCode, LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            return Denied("Latipay retry is available only for NZD orders.");

        if (!_latipaySubPaymentMethodService.HasAnyEnabledMethods(_settings))
            return Denied("No Latipay payment options are currently enabled for retry.");

        var latestAttempt = await _latipayPaymentAttemptService.GetLatestByOrderIdAsync(order.Id);
        if (latestAttempt is null)
        {
            return Allowed("Choose a Latipay payment option to retry this order.");
        }

        if (latestAttempt.PaymentCompletedOnUtc.HasValue)
            return Denied("This order already has a confirmed successful Latipay payment attempt.");

        if (!latestAttempt.RedirectCreatedOnUtc.HasValue)
        {
            return Allowed("The previous Latipay session did not start successfully. You can try again now.");
        }

        var canRetry = await _latipayReconciliationService.CanRetryPaymentAsync(order, cancellationToken);
        if (canRetry)
            return Allowed("Choose a Latipay payment option to retry this order.");

        var latestStatus = _latipayTransactionStatusMapper.Normalize(latestAttempt.ExternalStatus);
        return latestStatus switch
        {
            LatipayTransactionStatus.Paid => Denied("A Latipay payment for this order has already been confirmed."),
            LatipayTransactionStatus.Pending or LatipayTransactionStatus.Unknown =>
                Denied("We are still confirming the latest Latipay payment attempt for this order. Please wait before retrying."),
            _ => Denied("This order is not currently in a safe retry state.")
        };
    }

    public async Task<bool> CanRetryAsync(Order order, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(order, cancellationToken);
        return result.CanRetry;
    }

    private static LatipayRetryEligibilityResult Allowed(string message)
    {
        return new LatipayRetryEligibilityResult
        {
            CanRetry = true,
            Message = message
        };
    }

    private static LatipayRetryEligibilityResult Denied(string message)
    {
        return new LatipayRetryEligibilityResult
        {
            CanRetry = false,
            Message = message
        };
    }
}
