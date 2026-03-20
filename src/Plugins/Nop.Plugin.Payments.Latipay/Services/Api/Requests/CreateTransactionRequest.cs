using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the documented hosted transaction request.
/// </summary>
public class CreateTransactionRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("wallet_id")]
    public string WalletId { get; set; }

    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; }

    [JsonPropertyName("amount")]
    public string Amount { get; set; }

    [JsonPropertyName("return_url")]
    public string ReturnUrl { get; set; }

    [JsonPropertyName("callback_url")]
    public string CallbackUrl { get; set; }

    [JsonPropertyName("backPage_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string BackPageUrl { get; set; }

    [JsonPropertyName("merchant_reference")]
    public string MerchantReference { get; set; }

    [JsonPropertyName("ip")]
    public string Ip { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; }

    [JsonPropertyName("present_qr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string PresentQr { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SignatureValues =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = Amount,
            ["backPage_url"] = BackPageUrl,
            ["callback_url"] = CallbackUrl,
            ["ip"] = Ip,
            ["merchant_reference"] = MerchantReference,
            ["payment_method"] = PaymentMethod,
            ["present_qr"] = PresentQr,
            ["product_name"] = ProductName,
            ["return_url"] = ReturnUrl,
            ["user_id"] = UserId,
            ["version"] = Version,
            ["wallet_id"] = WalletId
        };
}
