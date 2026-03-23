using System.Text.Json.Serialization;
using WS.Plugin.Payments.Latipay.Services.Api.Serialization;

namespace WS.Plugin.Payments.Latipay.Services.Api.Responses;

/// <summary>
/// Represents the documented refund response.
/// </summary>
public class RefundResponse
{
    [JsonPropertyName("code")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Code?.Trim(), "0", StringComparison.OrdinalIgnoreCase);
}
