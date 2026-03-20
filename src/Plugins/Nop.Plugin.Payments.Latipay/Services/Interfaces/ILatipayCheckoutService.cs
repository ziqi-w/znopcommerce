using Nop.Plugin.Payments.Latipay.Services.Models;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Starts hosted Latipay checkout attempts.
/// </summary>
public interface ILatipayCheckoutService
{
    Task<LatipayHostedPaymentStartResult> StartHostedPaymentAsync(int orderId, string selectedSubPaymentMethod, CancellationToken cancellationToken = default);
}
