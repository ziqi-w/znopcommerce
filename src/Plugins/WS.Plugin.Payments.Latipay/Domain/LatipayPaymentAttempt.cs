using Nop.Core;

namespace WS.Plugin.Payments.Latipay.Domain;

/// <summary>
/// Represents a Latipay payment attempt for a nopCommerce order.
/// </summary>
public class LatipayPaymentAttempt : BaseEntity
{
    public int OrderId { get; set; }

    public int AttemptNumber { get; set; }

    public string MerchantReference { get; set; }

    public string SelectedSubPaymentMethod { get; set; }

    public string LatipayOrderId { get; set; }

    public string ExternalStatus { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public DateTime? RedirectCreatedOnUtc { get; set; }

    public DateTime? CallbackReceivedOnUtc { get; set; }

    public bool CallbackVerified { get; set; }

    public string CallbackIdempotencyKey { get; set; }

    public DateTime? PaymentCompletedOnUtc { get; set; }

    public DateTime? LastQueriedOnUtc { get; set; }

    public int? RetryOfPaymentAttemptId { get; set; }

    /// <summary>
    /// Stores a short redacted failure summary, not a raw callback or API payload.
    /// </summary>
    public string FailureReasonSummary { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public DateTime UpdatedOnUtc { get; set; }
}
