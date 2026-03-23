using WS.Plugin.Payments.Latipay.Domain;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Provides persistence operations for payment attempts.
/// </summary>
public interface ILatipayPaymentAttemptService
{
    Task<LatipayPaymentAttempt> GetByIdAsync(int id);

    Task<LatipayPaymentAttempt> GetByMerchantReferenceAsync(string merchantReference);

    Task<LatipayPaymentAttempt> GetByLatipayOrderIdAsync(string latipayOrderId);

    Task<LatipayPaymentAttempt> GetByCallbackIdempotencyKeyAsync(string callbackIdempotencyKey);

    Task<LatipayPaymentAttempt> GetLatestByOrderIdAsync(int orderId);

    Task<IList<LatipayPaymentAttempt>> GetByOrderIdAsync(int orderId);

    Task<IList<LatipayPaymentAttempt>> GetByRetryOfPaymentAttemptIdAsync(int retryOfPaymentAttemptId);

    Task<IList<LatipayPaymentAttempt>> GetUnresolvedAttemptsAsync(int maxCount = 100);

    Task<int> GetNextAttemptNumberAsync(int orderId);

    Task InsertAsync(LatipayPaymentAttempt paymentAttempt);

    Task UpdateAsync(LatipayPaymentAttempt paymentAttempt);
}
