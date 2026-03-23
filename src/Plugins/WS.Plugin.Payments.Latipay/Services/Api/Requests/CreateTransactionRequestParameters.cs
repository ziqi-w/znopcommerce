namespace WS.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the dynamic values required to build a Latipay create transaction request.
/// </summary>
public class CreateTransactionRequestParameters
{
    public string SubPaymentMethodKey { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; }

    public string ReturnUrl { get; set; }

    public string CallbackUrl { get; set; }

    public string BackPageUrl { get; set; }

    public string MerchantReference { get; set; }

    public string CustomerIpAddress { get; set; }

    public string ProductName { get; set; }

    public bool? PresentQr { get; set; }
}
