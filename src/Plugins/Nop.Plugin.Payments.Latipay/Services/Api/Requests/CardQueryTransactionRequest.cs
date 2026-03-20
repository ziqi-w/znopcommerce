using System.Globalization;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the documented hosted card query transaction request.
/// </summary>
public class CardQueryTransactionRequest
{
    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; }

    [JsonPropertyName("site_id")]
    public int SiteId { get; set; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SignatureValues =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["merchant_id"] = MerchantId,
            ["order_id"] = OrderId,
            ["site_id"] = SiteId.ToString(CultureInfo.InvariantCulture),
            ["timestamp"] = Timestamp.ToString(CultureInfo.InvariantCulture)
        };
}
