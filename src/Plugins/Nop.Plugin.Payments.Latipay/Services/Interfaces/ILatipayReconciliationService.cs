using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Latipay.Services.Models;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Reconciles uncertain Latipay attempts.
/// </summary>
public interface ILatipayReconciliationService
{
    Task<LatipayReconciliationResult> ReconcileByMerchantReferenceAsync(string merchantReference, string trigger, CancellationToken cancellationToken = default);

    Task<LatipayReconciliationResult> ReconcileLatestAttemptForOrderAsync(int orderId, string trigger, CancellationToken cancellationToken = default);

    Task<bool> CanRetryPaymentAsync(Order order, CancellationToken cancellationToken = default);

    Task ReconcilePendingAttemptsAsync(CancellationToken cancellationToken = default);
}
