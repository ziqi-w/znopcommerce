using WS.Plugin.Misc.AliyunOssStorage.Services.Models;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public interface IAliyunOssStorageService
{
    Task<AliyunOssOperationResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    Task<AliyunOssOperationResult> TestConnectionAsync(AliyunOssStorageSettings settings, CancellationToken cancellationToken = default);

    Task<AliyunOssObjectExistsResult> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<AliyunOssOperationResult> UploadObjectAsync(string objectKey, byte[] data, string contentType, CancellationToken cancellationToken = default);

    Task<AliyunOssOperationResult> UploadObjectAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<AliyunOssOperationResult> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<AliyunOssListObjectsResult> ListObjectKeysAsync(string prefix, CancellationToken cancellationToken = default);

    string BuildObjectKey(string thumbFileName);

    string BuildPublicUrl(string objectKey);

    string ResolveContentType(string fileName, string mimeType = null);
}
