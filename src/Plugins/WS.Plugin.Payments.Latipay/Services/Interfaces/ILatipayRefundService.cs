using Nop.Core.Domain.Orders;
using Nop.Services.Payments;
using WS.Plugin.Payments.Latipay.Services.Models;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Handles safe refund orchestration for Latipay orders.
/// </summary>
public interface ILatipayRefundService
{
    Task<LatipayRefundEligibilityResult> EvaluateEligibilityAsync(Order order,
        decimal refundAmount,
        bool isPartialRefund,
        CancellationToken cancellationToken = default);

    Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest, CancellationToken cancellationToken = default);
}
