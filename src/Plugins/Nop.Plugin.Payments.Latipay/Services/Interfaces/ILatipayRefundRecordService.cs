using Nop.Plugin.Payments.Latipay.Domain;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Provides persistence operations for refund records.
/// </summary>
public interface ILatipayRefundRecordService
{
    Task<LatipayRefundRecord> GetByIdAsync(int id);

    Task<LatipayRefundRecord> GetByRefundReferenceAsync(string refundReference);

    Task<IList<LatipayRefundRecord>> GetByOrderIdAsync(int orderId);

    Task<IList<LatipayRefundRecord>> GetByPaymentAttemptIdAsync(int paymentAttemptId);

    Task<decimal> GetTotalRefundAmountAsync(int orderId, params string[] refundStatuses);

    Task InsertAsync(LatipayRefundRecord refundRecord);

    Task UpdateAsync(LatipayRefundRecord refundRecord);
}
