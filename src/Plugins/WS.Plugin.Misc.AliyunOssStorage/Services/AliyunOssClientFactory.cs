using Aliyun.OSS;
using Aliyun.OSS.Common;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public class AliyunOssClientFactory : IAliyunOssClientFactory
{
    private readonly AliyunOssStorageSettings _aliyunOssStorageSettings;

    public AliyunOssClientFactory(AliyunOssStorageSettings aliyunOssStorageSettings)
    {
        _aliyunOssStorageSettings = aliyunOssStorageSettings;
    }

    public OssClient CreateClient()
    {
        return CreateClient(_aliyunOssStorageSettings);
    }

    public OssClient CreateClient(AliyunOssStorageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var endpoint = NormalizeEndpoint(settings.Endpoint, settings.UseHttps);
        var region = NormalizeRegion(settings.Region);
        var configuration = new ClientConfiguration
        {
            SignatureVersion = SignatureVersion.V4
        };

        var client = new OssClient(
            endpoint,
            settings.AccessKeyId?.Trim(),
            settings.AccessKeySecret?.Trim(),
            configuration);

        // OSS V4 signing requires the region ID itself, for example "ap-southeast-1",
        // even if the admin entered an endpoint-shaped value in the settings UI.
        client.SetRegion(region);

        return client;
    }

    internal static string NormalizeEndpoint(string endpoint, bool useHttps)
    {
        var normalized = endpoint?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString().TrimEnd('/');

        var scheme = useHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        return $"{scheme}://{normalized}";
    }

    internal static string NormalizeRegion(string region)
    {
        var normalized = region?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var regionUri))
            normalized = regionUri.Host;

        normalized = normalized.Trim().Trim('/');

        if (TryExtractRegionFromOssHost(normalized, out var extractedRegion))
            normalized = extractedRegion;
        else if (normalized.StartsWith("oss-", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];

        if (normalized.EndsWith("-internal", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^"-internal".Length];

        return normalized;
    }

    private static bool TryExtractRegionFromOssHost(string value, out string region)
    {
        region = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Trim('.').ToLowerInvariant();

        var markerIndex = normalized.IndexOf(".oss-", StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            region = normalized[(markerIndex + ".oss-".Length)..];
        }
        else if (normalized.StartsWith("oss-", StringComparison.Ordinal))
        {
            region = normalized["oss-".Length..];
        }
        else
        {
            return false;
        }

        var dotIndex = region.IndexOf('.');
        if (dotIndex >= 0)
            region = region[..dotIndex];

        if (string.IsNullOrWhiteSpace(region))
            return false;

        return true;
    }
}
