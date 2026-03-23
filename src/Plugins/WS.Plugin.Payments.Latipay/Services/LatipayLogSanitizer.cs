using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Api.Responses;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Produces redacted Latipay request and response log payloads.
/// </summary>
public static class LatipayLogSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Sanitize(CreateTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(new
        {
            user_id = MaskValue(request.UserId),
            wallet_id = MaskValue(request.WalletId),
            payment_method = request.PaymentMethod,
            amount = request.Amount,
            merchant_reference = request.MerchantReference,
            version = request.Version,
            product_name = request.ProductName,
            has_return_url = !string.IsNullOrWhiteSpace(request.ReturnUrl),
            has_callback_url = !string.IsNullOrWhiteSpace(request.CallbackUrl),
            has_back_page_url = !string.IsNullOrWhiteSpace(request.BackPageUrl),
            ip = MaskIpv4(request.Ip),
            signature = "<redacted>"
        }, JsonOptions);
    }

    public static string Sanitize(CardCreateTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(new
        {
            merchant_id = MaskValue(request.MerchantId),
            site_id = MaskValue(request.SiteId.ToString()),
            amount = request.Amount,
            order_id = request.OrderId,
            product_name = request.ProductName,
            currency = request.Currency,
            has_country = !string.IsNullOrWhiteSpace(request.Country),
            has_return_url = !string.IsNullOrWhiteSpace(request.ReturnUrl),
            has_callback_url = !string.IsNullOrWhiteSpace(request.CallbackUrl),
            has_cancel_order_url = !string.IsNullOrWhiteSpace(request.CancelOrderUrl),
            has_payer_details = true,
            signature = "<redacted>"
        }, JsonOptions);
    }

    public static string Sanitize(QueryTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(new
        {
            merchant_reference = request.MerchantReference,
            user_id = MaskValue(request.UserId),
            is_block = request.IsBlock,
            signature = "<redacted>"
        }, JsonOptions);
    }

    public static string Sanitize(CardQueryTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(new
        {
            merchant_id = MaskValue(request.MerchantId),
            site_id = MaskValue(request.SiteId.ToString()),
            order_id = request.OrderId,
            timestamp = request.Timestamp,
            signature = "<redacted>"
        }, JsonOptions);
    }

    public static string Sanitize(RefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(new
        {
            user_id = MaskValue(request.UserId),
            order_id = request.OrderId,
            refund_amount = request.RefundAmount,
            reference = request.Reference,
            signature = "<redacted>"
        }, JsonOptions);
    }

    public static string Sanitize(CardRefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return JsonSerializer.Serialize(new
        {
            merchant_id = MaskValue(request.MerchantId),
            site_id = MaskValue(request.SiteId.ToString()),
            order_id = request.OrderId,
            refund_amount = request.RefundAmount,
            has_reason = !string.IsNullOrWhiteSpace(request.Reason),
            signature = "<redacted>"
        }, JsonOptions);
    }

    public static string Sanitize(CreateTransactionResponse response, bool signatureValid)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(new
        {
            code = response.Code,
            message = response.Message,
            has_host_url = !string.IsNullOrWhiteSpace(response.HostUrl),
            has_nonce = !string.IsNullOrWhiteSpace(response.Nonce),
            signature_valid = signatureValid
        }, JsonOptions);
    }

    public static string Sanitize(CardCreateTransactionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(new
        {
            code = response.Code,
            message = response.Message,
            has_host_url = !string.IsNullOrWhiteSpace(response.HostUrl),
            has_nonce = !string.IsNullOrWhiteSpace(response.Nonce),
            is_success = response.IsSuccess
        }, JsonOptions);
    }

    public static string Sanitize(QueryTransactionResponse response, bool signatureValid)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(new
        {
            merchant_reference = response.MerchantReference,
            currency = response.Currency,
            amount = response.Amount,
            payment_method = response.PaymentMethod,
            normalized_method = response.NormalizedProviderPaymentMethod,
            status = response.Status,
            normalized_status = response.NormalizedStatus.ToString(),
            order_id = response.OrderId,
            signature_valid = signatureValid
        }, JsonOptions);
    }

    public static string Sanitize(CardQueryTransactionResponse response, bool signatureValid)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(new
        {
            code = response.Code,
            message = response.Message,
            order_id = response.OrderId,
            merchant_id = MaskValue(response.MerchantId),
            site_id = MaskValue(response.SiteId),
            currency = response.Currency,
            amount = response.Amount,
            status = response.Status,
            refund_flag = response.RefundFlag,
            has_pay_time = !string.IsNullOrWhiteSpace(response.PayTime),
            signature_valid = signatureValid
        }, JsonOptions);
    }

    public static string Sanitize(RefundResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(new
        {
            code = response.Code,
            message = response.Message,
            is_success = response.IsSuccess
        }, JsonOptions);
    }

    public static string Sanitize(CardRefundResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return JsonSerializer.Serialize(new
        {
            code = response.Code,
            message = response.Message,
            is_success = response.IsSuccess
        }, JsonOptions);
    }

    public static string SanitizeHttpFailure(HttpStatusCode statusCode, string responseBody)
    {
        return JsonSerializer.Serialize(new
        {
            status_code = (int)statusCode,
            body_excerpt = SanitizeArbitraryBody(responseBody)
        }, JsonOptions);
    }

    private static string SanitizeArbitraryBody(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        try
        {
            var node = JsonNode.Parse(value);
            if (node is null)
                return Truncate(value, 500);

            SanitizeNode(node);
            return Truncate(node.ToJsonString(JsonOptions), 500);
        }
        catch (JsonException)
        {
            return Truncate(value, 500);
        }
    }

    private static void SanitizeNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject.ToList())
                {
                    if (property.Value is null)
                        continue;

                    if (ShouldFullyRedact(property.Key))
                    {
                        jsonObject[property.Key] = "<redacted>";
                        continue;
                    }

                    if (ShouldMask(property.Key))
                    {
                        jsonObject[property.Key] = MaskValue(property.Value.ToString());
                        continue;
                    }

                    if (ShouldMaskIpv4(property.Key))
                    {
                        jsonObject[property.Key] = MaskIpv4(property.Value.ToString());
                        continue;
                    }

                    SanitizeNode(property.Value);
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    if (item is not null)
                        SanitizeNode(item);
                }

                break;
        }
    }

    private static bool ShouldFullyRedact(string propertyName)
    {
        return propertyName.Equals("signature", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("api_key", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("private_key", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("secret", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("address", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("email", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("phone", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("firstname", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("lastname", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldMask(string propertyName)
    {
        return propertyName.Equals("user_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("wallet_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("merchant_id", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("site_id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldMaskIpv4(string propertyName)
    {
        return propertyName.Equals("ip", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("client_ip", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskIpv4(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var parsedIp) || parsedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return "<redacted>";

        var bytes = parsedIp.GetAddressBytes();
        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.xxx";
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        var trimmed = value.Trim();
        return trimmed.Length <= 4
            ? new string('*', trimmed.Length)
            : $"{trimmed[..2]}***{trimmed[^2..]}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}
