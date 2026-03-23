using System.Security.Cryptography;
using System.Text;
using System.Net;
using WS.Plugin.Payments.Latipay.Services.Interfaces;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Generates and validates documented Latipay signatures.
/// </summary>
public class LatipaySignatureService : ILatipaySignatureService
{
    public string CreateRequestSignature(IDictionary<string, string> values, string secret)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalizedSecret = NormalizeSecret(secret);
        var payload = string.Join("&", values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}")) + normalizedSecret;

        return Compute(payload, normalizedSecret);
    }

    public string CreateSortedParameterSignature(IDictionary<string, string> values, string secret, bool urlEncodeValues = false)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalizedSecret = NormalizeSecret(secret);
        var payload = string.Join("&", values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={NormalizeValue(pair.Value, urlEncodeValues)}"));

        return Compute(payload, normalizedSecret);
    }

    public string CreateHostedResponseSignature(string nonce, string hostUrl, string secret)
    {
        return Compute($"{nonce}{hostUrl}", NormalizeSecret(secret));
    }

    public string CreateStatusSignature(string merchantReference, string paymentMethod, string status, string currency, string amount, string secret)
    {
        return Compute($"{merchantReference}{paymentMethod}{status}{currency}{amount}", NormalizeSecret(secret));
    }

    public bool IsHostedResponseSignatureValid(string nonce, string hostUrl, string signature, string secret)
    {
        return AreEqual(CreateHostedResponseSignature(nonce, hostUrl, secret), NormalizeSignature(signature));
    }

    public bool IsStatusSignatureValid(string merchantReference, string paymentMethod, string status, string currency, string amount, string signature, string secret)
    {
        return AreEqual(CreateStatusSignature(merchantReference, paymentMethod, status, currency, amount, secret), NormalizeSignature(signature));
    }

    public bool IsSortedParameterSignatureValid(IDictionary<string, string> values, string signature, string secret, bool urlEncodeValues = false)
    {
        ArgumentNullException.ThrowIfNull(values);

        return AreEqual(CreateSortedParameterSignature(values, secret, urlEncodeValues), NormalizeSignature(signature));
    }

    public bool AreEqual(string left, string right)
    {
        if (left is null || right is null)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
    }

    protected virtual string Compute(string payload, string secret)
    {
        var normalizedSecret = NormalizeSecret(secret);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(normalizedSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeSecret(string secret)
    {
        return string.IsNullOrWhiteSpace(secret)
            ? throw new ArgumentException("Latipay API key must not be empty.", nameof(secret))
            : secret.Trim();
    }

    private static string NormalizeSignature(string signature)
    {
        return string.IsNullOrWhiteSpace(signature)
            ? string.Empty
            : signature.Trim().ToLowerInvariant();
    }

    private static string NormalizeValue(string value, bool urlEncodeValue)
    {
        var normalizedValue = value.Trim();
        return urlEncodeValue
            ? WebUtility.UrlEncode(normalizedValue)
            : normalizedValue;
    }
}
