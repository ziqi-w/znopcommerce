using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Latipay.Domain;
using Nop.Plugin.Payments.Latipay.Services.Models;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

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
