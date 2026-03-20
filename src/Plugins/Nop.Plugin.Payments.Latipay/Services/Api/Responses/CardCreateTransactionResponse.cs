using System.Text.Json.Serialization;
using Nop.Plugin.Payments.Latipay.Services.Api.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Responses;

/// <summary>
/// Represents the documented hosted card create transaction response.
/// </summary>
public class CardCreateTransactionResponse
{
    [JsonPropertyName("code")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Code { get; set; }

    [JsonPropertyName("msg")]
    public string Message { get; set; }

    [JsonPropertyName("host_url")]
    public string HostUrl { get; set; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Code?.Trim(), "0", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string HostedPaymentUrl
    {
        get
        {
            if (!Uri.TryCreate(HostUrl, UriKind.Absolute, out var hostUri) || string.IsNullOrWhiteSpace(Nonce))
                return string.Empty;

            var redirectBaseUri = hostUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                ? hostUri
                : new Uri($"{hostUri.AbsoluteUri}/");

            return new Uri(redirectBaseUri, Nonce.Trim()).AbsoluteUri;
        }
    }
}
