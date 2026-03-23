namespace WS.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the dynamic values required to build a Latipay hosted card transaction query request.
/// </summary>
public class CardQueryTransactionRequestParameters
{
    public string MerchantReference { get; set; }
}
