using System.Globalization;
using WS.Plugin.Payments.Latipay.Services.Api.Responses;

namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents a status payload received from Latipay callback, browser return, or server-side query.
/// </summary>
public class LatipayStatusNotification
{
    public string MerchantReference { get; set; }

    public string PaymentMethod { get; set; }

    public string NotifyVersion { get; set; }

    public string Status { get; set; }

    public string Currency { get; set; }

    public string Amount { get; set; }

    public string OrderId { get; set; }

    public string PayTime { get; set; }

    public string Signature { get; set; }

    public bool HasSignatureFields =>
        !string.IsNullOrWhiteSpace(MerchantReference)
        && !string.IsNullOrWhiteSpace(PaymentMethod)
        && !string.IsNullOrWhiteSpace(Status)
        && !string.IsNullOrWhiteSpace(Currency)
        && !string.IsNullOrWhiteSpace(Amount)
        && !string.IsNullOrWhiteSpace(Signature);

    public bool HasCardCallbackSignatureFields =>
        !string.IsNullOrWhiteSpace(NotifyVersion)
        && !string.IsNullOrWhiteSpace(MerchantReference)
        && !string.IsNullOrWhiteSpace(OrderId)
        && !string.IsNullOrWhiteSpace(PaymentMethod)
        && !string.IsNullOrWhiteSpace(Status)
        && !string.IsNullOrWhiteSpace(Currency)
        && !string.IsNullOrWhiteSpace(Amount)
        && !string.IsNullOrWhiteSpace(PayTime)
        && !string.IsNullOrWhiteSpace(Signature);

    public bool TryGetAmountValue(out decimal amount)
    {
        return decimal.TryParse(Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    public static LatipayStatusNotification FromQueryResponse(QueryTransactionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new LatipayStatusNotification
        {
            MerchantReference = response.MerchantReference,
            PaymentMethod = response.PaymentMethod,
            Status = response.Status,
            Currency = response.Currency,
            Amount = response.Amount,
            OrderId = response.OrderId,
            PayTime = response.PayTime,
            Signature = response.Signature
        };
    }

    public static LatipayStatusNotification FromCardQueryResponse(CardQueryTransactionResponse response, string merchantReference)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new LatipayStatusNotification
        {
            MerchantReference = string.IsNullOrWhiteSpace(merchantReference)
                ? null
                : merchantReference.Trim(),
            PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.CardVm,
            Status = response.Status,
            Currency = response.Currency,
            Amount = response.Amount,
            OrderId = string.Equals(response.OrderId?.Trim(), merchantReference?.Trim(), StringComparison.OrdinalIgnoreCase)
                ? null
                : response.OrderId,
            PayTime = response.PayTime,
            Signature = response.Signature
        };
    }
}
