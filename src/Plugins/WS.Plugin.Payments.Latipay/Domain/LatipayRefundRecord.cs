using Nop.Core;

namespace WS.Plugin.Payments.Latipay.Domain;

/// <summary>
/// Represents a persisted refund record for a Latipay order.
/// </summary>
public class LatipayRefundRecord : BaseEntity
{
    public int OrderId { get; set; }

    public int PaymentAttemptId { get; set; }

    public string LatipayOrderId { get; set; }

    public string RefundReference { get; set; }

    public decimal RefundAmount { get; set; }

    public string RefundStatus { get; set; }

    public DateTime RequestedOnUtc { get; set; }

    public DateTime? CompletedOnUtc { get; set; }

    /// <summary>
    /// Stores a short redacted response summary, not a raw gateway payload.
    /// </summary>
    public string ExternalResponseSummary { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public DateTime UpdatedOnUtc { get; set; }
}
