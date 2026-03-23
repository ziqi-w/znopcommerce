namespace WS.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the dynamic values required to build a Latipay hosted card refund request.
/// </summary>
public class CardRefundRequestParameters
{
    public string LatipayOrderId { get; set; }

    public decimal RefundAmount { get; set; }

    public string Reason { get; set; }
}
