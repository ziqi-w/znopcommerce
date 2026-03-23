namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents the outcome of browser return processing.
/// </summary>
public class LatipayReturnProcessResult
{
    public bool IsConfirmedPaid { get; set; }

    public int? OrderId { get; set; }

    public string MerchantReference { get; set; }

    public string Status { get; set; }

    public string Message { get; set; }
}
