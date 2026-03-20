namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the dynamic values required to build a Latipay hosted card transaction request.
/// </summary>
public class CardCreateTransactionRequestParameters
{
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; }

    public string MerchantReference { get; set; }

    public string ProductName { get; set; }

    public string ReturnUrl { get; set; }

    public string CallbackUrl { get; set; }

    public string CancelOrderUrl { get; set; }

    public CardPayerDetails Payer { get; set; }
}
