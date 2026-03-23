namespace WS.Plugin.Payments.Latipay.Domain.Enums;

/// <summary>
/// Represents local refund processing states persisted by the plugin.
/// </summary>
public enum LatipayRefundStatus
{
    PendingSubmission = 10,
    Succeeded = 20,
    Failed = 30,
    ReviewRequired = 40
}
