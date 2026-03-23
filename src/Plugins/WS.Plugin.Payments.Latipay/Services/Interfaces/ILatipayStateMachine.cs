using Nop.Core.Domain.Orders;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Services.Models;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Applies verified external payment state updates.
/// </summary>
public interface ILatipayStateMachine
{
    Task<LatipayStateTransitionResult> ApplyVerifiedStatusAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipayStatusNotification notification,
        string source,
        CancellationToken cancellationToken = default);
}
