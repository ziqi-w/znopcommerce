namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents the outcome of a reconciliation attempt.
/// </summary>
public class LatipayReconciliationResult
{
    public bool IsVerified { get; set; }

    public bool IsPaid { get; set; }

    public bool KeepPending { get; set; }

    public bool ReviewRequired { get; set; }

    public int? OrderId { get; set; }

    public string MerchantReference { get; set; }

    public string ExternalStatus { get; set; }

    public string Message { get; set; }
}
