using WS.Plugin.Payments.Latipay.Services.Models;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Handles browser return processing.
/// </summary>
public interface ILatipayReturnService
{
    Task<LatipayReturnProcessResult> ProcessReturnAsync(LatipayStatusNotification notification, CancellationToken cancellationToken = default);
}
