using Nop.Plugin.Payments.Latipay.Domain.Enums;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;

namespace Nop.Plugin.Payments.Latipay.Services;

/// <summary>
/// Maps Latipay raw transaction statuses to internal normalized values.
/// </summary>
public class LatipayTransactionStatusMapper : ILatipayTransactionStatusMapper
{
    public LatipayTransactionStatus Normalize(string rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return LatipayTransactionStatus.Unknown;

        return rawStatus.Trim().ToLowerInvariant() switch
        {
            "pending" => LatipayTransactionStatus.Pending,
            "paid" => LatipayTransactionStatus.Paid,
            "failed" => LatipayTransactionStatus.Failed,
            "cancel_or_fail" => LatipayTransactionStatus.Failed,
            "canceled" => LatipayTransactionStatus.Canceled,
            "cancelled" => LatipayTransactionStatus.Canceled,
            "rejected" => LatipayTransactionStatus.Rejected,
            _ => LatipayTransactionStatus.Unknown
        };
    }
}
