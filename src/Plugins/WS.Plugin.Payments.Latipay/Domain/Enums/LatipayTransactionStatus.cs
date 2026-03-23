namespace WS.Plugin.Payments.Latipay.Domain.Enums;

/// <summary>
/// Represents normalized transaction statuses.
/// </summary>
public enum LatipayTransactionStatus
{
    Unknown = 0,
    Pending = 10,
    Paid = 20,
    Failed = 30,
    Canceled = 40,
    Rejected = 50
}
