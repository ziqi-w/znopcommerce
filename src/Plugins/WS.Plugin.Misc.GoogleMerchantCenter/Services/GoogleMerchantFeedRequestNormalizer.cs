namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

internal static class GoogleMerchantFeedRequestNormalizer
{
    public static string NormalizeCurrencyCode(string value)
    {
        return NormalizeIsoCode(value, GoogleMerchantCenterDefaults.MaxCurrencyCodeLength);
    }

    public static string NormalizeCountryCode(string value)
    {
        return NormalizeIsoCode(value, GoogleMerchantCenterDefaults.MaxCountryCodeLength);
    }

    public static string NormalizeSnapshotSegment(string value, string fallback)
    {
        var normalized = NormalizeSafeUpperToken(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? fallback
            : normalized;
    }

    private static string NormalizeIsoCode(string value, int requiredLength)
    {
        var normalized = NormalizeSafeUpperToken(value);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != requiredLength)
            return null;

        return normalized;
    }

    private static string NormalizeSafeUpperToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsAsciiLetter)
            .ToArray());

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}
