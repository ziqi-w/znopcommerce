using System.Globalization;
using System.Text.Json.Serialization;
using WS.Plugin.Payments.Latipay.Services.Api.Serialization;

namespace WS.Plugin.Payments.Latipay.Services.Api.Responses;

/// <summary>
/// Represents the documented hosted card query transaction response.
/// </summary>
public class CardQueryTransactionResponse
{
    [JsonPropertyName("code")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Code { get; set; }

    [JsonPropertyName("msg")]
    public string Message { get; set; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; }

    [JsonPropertyName("site_id")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string SiteId { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("amount")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("refund_flag")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string RefundFlag { get; set; }

    [JsonPropertyName("pay_time")]
    public string PayTime { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Code?.Trim(), "0", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SignatureValues =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = Amount,
            ["code"] = Code,
            ["currency"] = Currency,
            ["merchant_id"] = MerchantId,
            ["msg"] = Message,
            ["order_id"] = OrderId,
            ["pay_time"] = PayTime,
            ["refund_flag"] = RefundFlag,
            ["site_id"] = SiteId,
            ["status"] = Status
        };

    public bool TryGetAmountValue(out decimal amount)
    {
        return decimal.TryParse(Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
