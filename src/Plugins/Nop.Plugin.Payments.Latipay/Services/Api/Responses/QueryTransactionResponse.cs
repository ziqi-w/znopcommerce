using System.Globalization;
using System.Text.Json.Serialization;
using Nop.Plugin.Payments.Latipay.Domain.Enums;
using Nop.Plugin.Payments.Latipay.Services.Api.Serialization;

namespace Nop.Plugin.Payments.Latipay.Services.Api.Responses;

/// <summary>
/// Represents the documented transaction query response.
/// </summary>
public class QueryTransactionResponse
{
    [JsonPropertyName("merchant_reference")]
    public string MerchantReference { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("amount")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Amount { get; set; }

    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("pay_time")]
    public string PayTime { get; set; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonIgnore]
    public LatipayTransactionStatus NormalizedStatus { get; set; } = LatipayTransactionStatus.Unknown;

    [JsonIgnore]
    public string NormalizedSubPaymentMethodKey { get; set; }

    [JsonIgnore]
    public string NormalizedProviderPaymentMethod { get; set; }

    public bool TryGetAmountValue(out decimal amount)
    {
        return decimal.TryParse(Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
