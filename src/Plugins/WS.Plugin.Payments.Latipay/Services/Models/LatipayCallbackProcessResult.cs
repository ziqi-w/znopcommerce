namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents the outcome of callback processing.
/// </summary>
public class LatipayCallbackProcessResult
{
    public bool AcknowledgeCallback { get; set; }

    public bool IsDuplicate { get; set; }

    public string MerchantReference { get; set; }

    public string Message { get; set; }
}
