using System.Globalization;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the documented hosted card create transaction request.
/// </summary>
public class CardCreateTransactionRequest
{
    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; }

    [JsonPropertyName("site_id")]
    public int SiteId { get; set; }

    [JsonPropertyName("amount")]
    public string Amount { get; set; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("firstname")]
    public string FirstName { get; set; }

    [JsonPropertyName("lastname")]
    public string LastName { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Country { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("postcode")]
    public string Postcode { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("phone")]
    public string Phone { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("return_url")]
    public string ReturnUrl { get; set; }

    [JsonPropertyName("callback_url")]
    public string CallbackUrl { get; set; }

    [JsonPropertyName("cancel_order_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string CancelOrderUrl { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SignatureValues =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["address"] = Address,
            ["amount"] = Amount,
            ["callback_url"] = CallbackUrl,
            ["cancel_order_url"] = CancelOrderUrl,
            ["city"] = City,
            ["country"] = Country,
            ["currency"] = Currency,
            ["email"] = Email,
            ["firstname"] = FirstName,
            ["lastname"] = LastName,
            ["merchant_id"] = MerchantId,
            ["order_id"] = OrderId,
            ["phone"] = Phone,
            ["postcode"] = Postcode,
            ["product_name"] = ProductName,
            ["return_url"] = ReturnUrl,
            ["site_id"] = SiteId.ToString(CultureInfo.InvariantCulture),
            ["state"] = State,
            ["timestamp"] = Timestamp.ToString(CultureInfo.InvariantCulture)
        };
}
