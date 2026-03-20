using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the documented transaction query request.
/// </summary>
public class QueryTransactionRequest
{
    [JsonIgnore]
    public string MerchantReference { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("is_block")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string IsBlock { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> SignatureValues =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["is_block"] = IsBlock,
            ["merchant_reference"] = MerchantReference,
            ["user_id"] = UserId
        };
}
