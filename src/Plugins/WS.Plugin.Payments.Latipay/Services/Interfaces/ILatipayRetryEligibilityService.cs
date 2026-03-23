using Nop.Core.Domain.Orders;
using WS.Plugin.Payments.Latipay.Services.Models;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Determines whether an existing nopCommerce order can safely retry Latipay payment.
/// </summary>
public interface ILatipayRetryEligibilityService
{
    Task<LatipayRetryEligibilityResult> EvaluateAsync(Order order, CancellationToken cancellationToken = default);

    Task<bool> CanRetryAsync(Order order, CancellationToken cancellationToken = default);
}
