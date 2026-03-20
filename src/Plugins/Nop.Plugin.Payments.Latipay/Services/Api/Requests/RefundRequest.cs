using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the documented refund request.
/// </summary>
public class RefundRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("refund_amount")]
    public string RefundAmount { get; set; }

    [JsonPropertyName("reference")]
    public string Reference { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SignatureValues =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_id"] = OrderId,
            ["reference"] = Reference,
            ["refund_amount"] = RefundAmount,
            ["user_id"] = UserId
        };
}
