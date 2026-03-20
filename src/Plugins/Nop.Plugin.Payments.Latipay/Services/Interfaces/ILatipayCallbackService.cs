using Nop.Plugin.Payments.Latipay.Services.Models;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Handles callback processing.
/// </summary>
public interface ILatipayCallbackService
{
    Task<LatipayCallbackProcessResult> ProcessCallbackAsync(LatipayStatusNotification notification, CancellationToken cancellationToken = default);
}
