namespace WS.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the dynamic values required to build a Latipay refund request.
/// </summary>
public class RefundRequestParameters
{
    public string OrderId { get; set; }

    public decimal RefundAmount { get; set; }

    public string Reference { get; set; }
}
