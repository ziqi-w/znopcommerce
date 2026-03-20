using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Responses;

/// <summary>
/// Represents the documented hosted transaction response.
/// </summary>
public class CreateTransactionResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("host_url")]
    public string HostUrl { get; set; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

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
