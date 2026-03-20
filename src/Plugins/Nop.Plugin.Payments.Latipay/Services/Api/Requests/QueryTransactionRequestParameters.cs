namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the dynamic values required to build a Latipay transaction query request.
/// </summary>
public class QueryTransactionRequestParameters
{
    public string MerchantReference { get; set; }

    public bool? IsBlock { get; set; }
}
