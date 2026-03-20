using Nop.Plugin.Payments.Latipay.Services.Models;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Handles browser return processing.
/// </summary>
public interface ILatipayReturnService
{
    Task<LatipayReturnProcessResult> ProcessReturnAsync(LatipayStatusNotification notification, CancellationToken cancellationToken = default);
}
