using Aliyun.OSS;
using Aliyun.OSS.Common;
using Microsoft.AspNetCore.StaticFiles;
using Nop.Services.Logging;
using System.Text;
using WS.Plugin.Misc.AliyunOssStorage.Services.Models;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public class AliyunOssStorageService : IAliyunOssStorageService
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    private static readonly Dictionary<string, string> KnownImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".avif"] = "image/avif",
        [".bmp"] = "image/bmp",
        [".gif"] = "image/gif",
        [".ico"] = "image/x-icon",
        [".jpeg"] = "image/jpeg",
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
        [".svg"] = "image/svg+xml",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".webp"] = "image/webp"
    };

    private readonly IAliyunOssClientFactory _aliyunOssClientFactory;
    private readonly AliyunOssStorageSettings _aliyunOssStorageSettings;
    private readonly ILogger _logger;

    public AliyunOssStorageService(IAliyunOssClientFactory aliyunOssClientFactory,
        AliyunOssStorageSettings aliyunOssStorageSettings,
        ILogger logger)
    {
        _aliyunOssClientFactory = aliyunOssClientFactory;
        _aliyunOssStorageSettings = aliyunOssStorageSettings;
        _logger = logger;
    }

    public async Task<AliyunOssOperationResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await TestConnectionAsync(_aliyunOssStorageSettings, cancellationToken);
    }

    public async Task<AliyunOssOperationResult> TestConnectionAsync(AliyunOssStorageSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var validationError = ValidateSettings(settings);
        if (validationError is not null)
            return AliyunOssOperationResult.Failure(validationError);

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var client = _aliyunOssClientFactory.CreateClient(settings);
                var bucketName = settings.BucketName.Trim();
                if (!client.DoesBucketExist(bucketName))
                    throw new InvalidOperationException("The configured OSS bucket could not be found or is not accessible.");

                var connectionTestPrefix = BuildObjectKey(settings, ".connection-test");
                CleanupConnectionTestObjects(client, bucketName, connectionTestPrefix, cancellationToken);

                var testObjectKey = BuildObjectKey(settings, $".connection-test/{Guid.NewGuid():N}.txt");
                var payload = Encoding.UTF8.GetBytes("nopCommerce Aliyun OSS thumbnail connection test");
                using var stream = new MemoryStream(payload, writable: false);

                var metadata = new ObjectMetadata
                {
                    ContentType = "text/plain"
                };

                client.PutObject(bucketName, testObjectKey, stream, metadata);
                DeleteConnectionTestObject(client, bucketName, testObjectKey, cancellationToken);
            }, cancellationToken);

            return AliyunOssOperationResult.Success();
        }
        catch (OssException exception)
        {
            var errorMessage = FormatOssExceptionMessage("test OSS read/write connectivity", exception);
            await _logger.WarningAsync(BuildOperationMessage(settings, "test OSS read/write connectivity", errorMessage), exception);
            return AliyunOssOperationResult.Failure(errorMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            await _logger.WarningAsync(BuildOperationMessage(settings, "test OSS read/write connectivity", exception.Message), exception);
            return AliyunOssOperationResult.Failure(exception.Message);
        }
        catch (Exception exception)
        {
            const string errorMessage = "Unexpected error while testing OSS read/write connectivity.";
            await _logger.ErrorAsync(BuildOperationMessage(settings, "test OSS read/write connectivity", errorMessage), exception);
            return AliyunOssOperationResult.Failure(errorMessage);
        }
    }

    public async Task<AliyunOssObjectExistsResult> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return AliyunOssObjectExistsResult.Failure("The object key is required.");

        var validationError = ValidateSettings(_aliyunOssStorageSettings);
        if (validationError is not null)
            return AliyunOssObjectExistsResult.Failure(validationError);

        try
        {
            var exists = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var client = _aliyunOssClientFactory.CreateClient();
                return client.DoesObjectExist(_aliyunOssStorageSettings.BucketName.Trim(), objectKey);
            }, cancellationToken);

            return AliyunOssObjectExistsResult.Success(exists);
        }
        catch (OssException exception)
        {
            var errorMessage = FormatOssExceptionMessage($"check object existence for '{objectKey}'", exception);
            await _logger.ErrorAsync(BuildOperationMessage($"check object existence for '{objectKey}'", errorMessage), exception);
            return AliyunOssObjectExistsResult.Failure(errorMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Unexpected error while checking object existence for '{objectKey}'.";
            await _logger.ErrorAsync(BuildOperationMessage($"check object existence for '{objectKey}'", errorMessage), exception);
            return AliyunOssObjectExistsResult.Failure(errorMessage);
        }
    }

    public Task<AliyunOssOperationResult> UploadObjectAsync(string objectKey, byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        if (data is null || data.Length == 0)
            return Task.FromResult(AliyunOssOperationResult.Failure("The thumbnail payload is empty."));

        var stream = new MemoryStream(data, writable: false);
        return UploadObjectFromOwnedStreamAsync(objectKey, stream, contentType, cancellationToken);
    }

    public async Task<AliyunOssOperationResult> UploadObjectAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (content is null)
            return AliyunOssOperationResult.Failure("The content stream is required.");

        if (!content.CanRead)
            return AliyunOssOperationResult.Failure("The content stream must be readable.");

        if (content.CanSeek)
            content.Position = 0;

        return await UploadObjectInternalAsync(objectKey, content, contentType, ownsStream: false, cancellationToken);
    }

    public async Task<AliyunOssOperationResult> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return AliyunOssOperationResult.Failure("The object key is required.");

        var validationError = ValidateSettings(_aliyunOssStorageSettings);
        if (validationError is not null)
            return AliyunOssOperationResult.Failure(validationError);

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var client = _aliyunOssClientFactory.CreateClient();
                client.DeleteObject(_aliyunOssStorageSettings.BucketName.Trim(), objectKey);
            }, cancellationToken);

            return AliyunOssOperationResult.Success();
        }
        catch (OssException exception)
        {
            var errorMessage = FormatOssExceptionMessage($"delete object '{objectKey}'", exception);
            await _logger.ErrorAsync(BuildOperationMessage($"delete object '{objectKey}'", errorMessage), exception);
            return AliyunOssOperationResult.Failure(errorMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Unexpected error while deleting object '{objectKey}'.";
            await _logger.ErrorAsync(BuildOperationMessage($"delete object '{objectKey}'", errorMessage), exception);
            return AliyunOssOperationResult.Failure(errorMessage);
        }
    }

    public async Task<AliyunOssListObjectsResult> ListObjectKeysAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSettings(_aliyunOssStorageSettings);
        if (validationError is not null)
            return AliyunOssListObjectsResult.Failure(validationError);

        try
        {
            var keys = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var client = _aliyunOssClientFactory.CreateClient();
                var bucketName = _aliyunOssStorageSettings.BucketName.Trim();
                var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
                var keysBuffer = new List<string>();
                var marker = string.Empty;

                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var request = new ListObjectsRequest(bucketName)
                    {
                        Prefix = normalizedPrefix,
                        Marker = marker,
                        MaxKeys = 1000
                    };

                    var listing = client.ListObjects(request);
                    foreach (var summary in listing.ObjectSummaries)
                    {
                        if (!string.IsNullOrWhiteSpace(summary.Key))
                            keysBuffer.Add(summary.Key);
                    }

                    marker = listing.NextMarker;

                    if (!listing.IsTruncated)
                        break;
                } while (true);

                return (IReadOnlyCollection<string>)keysBuffer;
            }, cancellationToken);

            return AliyunOssListObjectsResult.Success(keys);
        }
        catch (OssException exception)
        {
            var errorMessage = FormatOssExceptionMessage($"list objects with prefix '{prefix}'", exception);
            await _logger.ErrorAsync(BuildOperationMessage($"list objects with prefix '{prefix}'", errorMessage), exception);
            return AliyunOssListObjectsResult.Failure(errorMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Unexpected error while listing objects with prefix '{prefix}'.";
            await _logger.ErrorAsync(BuildOperationMessage($"list objects with prefix '{prefix}'", errorMessage), exception);
            return AliyunOssListObjectsResult.Failure(errorMessage);
        }
    }

    public string BuildObjectKey(string thumbFileName)
    {
        return BuildObjectKey(_aliyunOssStorageSettings, thumbFileName);
    }

    private string BuildObjectKey(AliyunOssStorageSettings settings, string thumbFileName)
    {
        var normalizedFileName = NormalizeObjectKeyPart(thumbFileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName))
            return string.Empty;

        var normalizedPrefix = NormalizeObjectKeyPart(settings.BaseThumbPathPrefix);
        if (string.IsNullOrWhiteSpace(normalizedPrefix))
            return normalizedFileName;

        return $"{normalizedPrefix}/{normalizedFileName}";
    }

    public string BuildPublicUrl(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return string.Empty;

        var encodedObjectKey = EncodeObjectKeyForUrl(objectKey);
        var baseUrl = BuildBasePublicUrl();
        return string.IsNullOrEmpty(baseUrl) ? string.Empty : $"{baseUrl}/{encodedObjectKey}";
    }

    public string ResolveContentType(string fileName, string mimeType = null)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) &&
            !mimeType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return mimeType.Trim();
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension) &&
            KnownImageContentTypes.TryGetValue(extension, out var knownContentType))
        {
            return knownContentType;
        }

        if (!string.IsNullOrWhiteSpace(fileName) &&
            ContentTypeProvider.TryGetContentType(fileName, out var detectedContentType))
        {
            return detectedContentType;
        }

        return "application/octet-stream";
    }

    private Task<AliyunOssOperationResult> UploadObjectFromOwnedStreamAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
    {
        return UploadObjectInternalAsync(objectKey, content, contentType, ownsStream: true, cancellationToken);
    }

    private async Task<AliyunOssOperationResult> UploadObjectInternalAsync(string objectKey, Stream content, string contentType, bool ownsStream, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return AliyunOssOperationResult.Failure("The object key is required.");

        var validationError = ValidateSettings(_aliyunOssStorageSettings);
        if (validationError is not null)
        {
            if (ownsStream)
                await content.DisposeAsync();

            return AliyunOssOperationResult.Failure(validationError);
        }

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (content.CanSeek)
                    content.Position = 0;

                var metadata = new ObjectMetadata
                {
                    ContentType = ResolveContentType(objectKey, contentType)
                };

                var client = _aliyunOssClientFactory.CreateClient();
                client.PutObject(_aliyunOssStorageSettings.BucketName.Trim(), objectKey, content, metadata);
            }, cancellationToken);

            return AliyunOssOperationResult.Success();
        }
        catch (OssException exception)
        {
            var errorMessage = FormatOssExceptionMessage($"upload object '{objectKey}'", exception);
            await _logger.ErrorAsync(BuildOperationMessage($"upload object '{objectKey}'", errorMessage), exception);
            return AliyunOssOperationResult.Failure(errorMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Unexpected error while uploading object '{objectKey}'.";
            await _logger.ErrorAsync(BuildOperationMessage($"upload object '{objectKey}'", errorMessage), exception);
            return AliyunOssOperationResult.Failure(errorMessage);
        }
        finally
        {
            if (ownsStream)
                await content.DisposeAsync();
        }
    }

    private string BuildBasePublicUrl()
    {
        if (!string.IsNullOrWhiteSpace(_aliyunOssStorageSettings.CustomBaseUrl))
            return _aliyunOssStorageSettings.CustomBaseUrl.Trim().TrimEnd('/');

        var endpoint = AliyunOssClientFactory.NormalizeEndpoint(_aliyunOssStorageSettings.Endpoint, _aliyunOssStorageSettings.UseHttps);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
            return string.Empty;

        var bucketName = _aliyunOssStorageSettings.BucketName?.Trim();
        if (string.IsNullOrWhiteSpace(bucketName))
            return string.Empty;

        var host = endpointUri.Host.StartsWith($"{bucketName}.", StringComparison.OrdinalIgnoreCase)
            ? endpointUri.Host
            : $"{bucketName}.{endpointUri.Host}";

        var uriBuilder = new UriBuilder(endpointUri.Scheme, host, endpointUri.IsDefaultPort ? -1 : endpointUri.Port);
        return uriBuilder.Uri.ToString().TrimEnd('/');
    }

    private string ValidateSettings(AliyunOssStorageSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            return "The OSS endpoint is not configured.";

        if (string.IsNullOrWhiteSpace(settings.BucketName))
            return "The OSS bucket name is not configured.";

        if (string.IsNullOrWhiteSpace(settings.Region))
            return "The OSS region is not configured.";

        if (string.IsNullOrWhiteSpace(settings.AccessKeyId))
            return "The OSS access key ID is not configured.";

        if (string.IsNullOrWhiteSpace(settings.AccessKeySecret))
            return "The OSS access key secret is not configured.";

        return null;
    }

    private string FormatOssExceptionMessage(string operation, OssException exception)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(exception.ErrorCode))
            parts.Add($"ErrorCode={exception.ErrorCode}");

        if (!string.IsNullOrWhiteSpace(exception.RequestId))
            parts.Add($"RequestId={exception.RequestId}");

        if (!string.IsNullOrWhiteSpace(exception.HostId))
            parts.Add($"HostId={exception.HostId}");

        var detail = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : string.Empty;
        return $"OSS failed to {operation}: {exception.Message}{detail}";
    }

    private string BuildOperationMessage(AliyunOssStorageSettings settings, string operation, string detail)
    {
        return $"{AliyunOssStorageDefaults.SystemName}: Unable to {operation} for bucket '{settings.BucketName}'. {detail}";
    }

    private string BuildOperationMessage(string operation, string detail)
    {
        return BuildOperationMessage(_aliyunOssStorageSettings, operation, detail);
    }

    private static void CleanupConnectionTestObjects(OssClient client, string bucketName, string prefix, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return;

        var marker = string.Empty;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new ListObjectsRequest(bucketName)
            {
                Prefix = prefix,
                Marker = marker,
                MaxKeys = 100
            };

            var listing = client.ListObjects(request);
            foreach (var summary in listing.ObjectSummaries)
            {
                if (string.IsNullOrWhiteSpace(summary.Key))
                    continue;

                DeleteConnectionTestObject(client, bucketName, summary.Key, cancellationToken);
            }

            marker = listing.NextMarker;

            if (!listing.IsTruncated)
                break;
        } while (true);
    }

    private static void DeleteConnectionTestObject(OssClient client, string bucketName, string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        client.DeleteObject(bucketName, objectKey);

        if (client.DoesObjectExist(bucketName, objectKey))
            throw new InvalidOperationException($"The temporary OSS connection test object '{objectKey}' could not be deleted.");
    }

    private static string NormalizeObjectKeyPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Replace('\\', '/');

        while (normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = normalized[1..];

        while (normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized[..^1];

        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

        return normalized;
    }

    private static string EncodeObjectKeyForUrl(string objectKey)
    {
        var segments = objectKey
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.EscapeDataString);

        return string.Join('/', segments);
    }
}
