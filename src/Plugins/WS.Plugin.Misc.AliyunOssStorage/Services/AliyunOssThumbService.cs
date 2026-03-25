using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Core.Caching;
using Nop.Services.Logging;
using Nop.Services.Media;
using WS.Plugin.Misc.AliyunOssStorage.Services.Models;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public class AliyunOssThumbService : IThumbService
{
    private readonly AliyunOssStorageSettings _aliyunOssStorageSettings;
    private readonly INopFileProvider _fileProvider;
    private readonly IAliyunOssStorageService _aliyunOssStorageService;
    private readonly ILogger _logger;
    private readonly IStaticCacheManager _staticCacheManager;
    private readonly ThumbService _thumbService;

    public AliyunOssThumbService(AliyunOssStorageSettings aliyunOssStorageSettings,
        INopFileProvider fileProvider,
        IAliyunOssStorageService aliyunOssStorageService,
        ILogger logger,
        IStaticCacheManager staticCacheManager,
        ThumbService thumbService)
    {
        _aliyunOssStorageSettings = aliyunOssStorageSettings;
        _fileProvider = fileProvider;
        _aliyunOssStorageService = aliyunOssStorageService;
        _logger = logger;
        _staticCacheManager = staticCacheManager;
        _thumbService = thumbService;
    }

    public async Task<string> GetThumbLocalPathAsync(string pictureUrl)
    {
        if (string.IsNullOrEmpty(pictureUrl))
            return string.Empty;

        return await GetThumbLocalPathByFileNameAsync(_fileProvider.GetFileName(pictureUrl));
    }

    public async Task<bool> GeneratedThumbExistsAsync(string thumbFilePath, string thumbFileName)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(AliyunOssStorageDefaults.ThumbExistsCacheKey, thumbFileName);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var objectKey = _aliyunOssStorageService.BuildObjectKey(thumbFileName);
            var remoteExistsResult = await _aliyunOssStorageService.ObjectExistsAsync(objectKey);

            if (remoteExistsResult.Succeeded)
                return remoteExistsResult.Exists;

            return await UseLocalFallbackForExistsAsync(thumbFileName, remoteExistsResult);
        });
    }

    public async Task SaveThumbAsync(string thumbFilePath, string thumbFileName, string mimeType, byte[] binary)
    {
        var objectKey = _aliyunOssStorageService.BuildObjectKey(thumbFileName);
        var contentType = _aliyunOssStorageService.ResolveContentType(thumbFileName, mimeType);
        var uploadResult = await _aliyunOssStorageService.UploadObjectAsync(objectKey, binary, contentType);

        if (uploadResult.Succeeded)
        {
            await _staticCacheManager.RemoveByPrefixAsync(AliyunOssStorageDefaults.ThumbsExistsPrefix);

            if (_aliyunOssStorageSettings.DeleteLocalThumbAfterUpload)
                await DeleteLocalThumbIfExistsAsync(thumbFileName);

            return;
        }

        if (!_aliyunOssStorageSettings.FallbackToLocalOnFailure)
            return;

        // Keep storefront thumbnail generation alive when OSS is temporarily unavailable.
        await _logger.WarningAsync(
            $"{AliyunOssStorageDefaults.SystemName}: Falling back to local thumbnail storage for '{thumbFileName}' because OSS upload failed. {uploadResult.ErrorMessage}");

        var localThumbPath = await _thumbService.GetThumbLocalPathByFileNameAsync(thumbFileName);
        await _thumbService.SaveThumbAsync(localThumbPath, thumbFileName, mimeType, binary);
        await _staticCacheManager.RemoveByPrefixAsync(AliyunOssStorageDefaults.ThumbsExistsPrefix);
    }

    public async Task<string> GetThumbLocalPathByFileNameAsync(string thumbFileName)
    {
        var objectKey = _aliyunOssStorageService.BuildObjectKey(thumbFileName);
        var remoteExistsResult = await _aliyunOssStorageService.ObjectExistsAsync(objectKey);
        if (remoteExistsResult is { Succeeded: true, Exists: true })
            return _aliyunOssStorageService.BuildPublicUrl(objectKey);

        var localThumbPath = await _thumbService.GetThumbLocalPathByFileNameAsync(thumbFileName);
        if (await ShouldUseLocalThumbAsync(thumbFileName, localThumbPath, remoteExistsResult))
            return localThumbPath;

        return _aliyunOssStorageService.BuildPublicUrl(objectKey);
    }

    public async Task<string> GetThumbUrlAsync(string thumbFileName, string storeLocation = null)
    {
        var objectKey = _aliyunOssStorageService.BuildObjectKey(thumbFileName);
        var remoteExistsResult = await _aliyunOssStorageService.ObjectExistsAsync(objectKey);
        if (remoteExistsResult is { Succeeded: true, Exists: true })
            return _aliyunOssStorageService.BuildPublicUrl(objectKey);

        var localThumbPath = await _thumbService.GetThumbLocalPathByFileNameAsync(thumbFileName);
        if (await ShouldUseLocalThumbAsync(thumbFileName, localThumbPath, remoteExistsResult))
            return await _thumbService.GetThumbUrlAsync(thumbFileName, storeLocation);

        return _aliyunOssStorageService.BuildPublicUrl(objectKey);
    }

    public async Task DeletePictureThumbsAsync(Picture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        var objectKeyPrefix = _aliyunOssStorageService.BuildObjectKey($"{picture.Id:0000000}");
        var listResult = await _aliyunOssStorageService.ListObjectKeysAsync(objectKeyPrefix);

        if (listResult.Succeeded)
        {
            foreach (var objectKey in listResult.ObjectKeys)
            {
                var deleteResult = await _aliyunOssStorageService.DeleteObjectAsync(objectKey);
                if (!deleteResult.Succeeded)
                {
                    await _logger.WarningAsync(
                        $"{AliyunOssStorageDefaults.SystemName}: Failed to delete OSS thumbnail object '{objectKey}'. {deleteResult.ErrorMessage}");
                }
            }

            await _staticCacheManager.RemoveByPrefixAsync(AliyunOssStorageDefaults.ThumbsExistsPrefix);
        }
        else if (!_aliyunOssStorageSettings.FallbackToLocalOnFailure)
        {
            await _logger.WarningAsync(
                $"{AliyunOssStorageDefaults.SystemName}: Unable to enumerate OSS thumbnail objects for picture ID {picture.Id}. {listResult.ErrorMessage}");
        }

        await _thumbService.DeletePictureThumbsAsync(picture);
    }

    private async Task<bool> ShouldUseLocalThumbAsync(string thumbFileName, string localThumbPath, AliyunOssObjectExistsResult remoteExistsResult)
    {
        if (!_aliyunOssStorageSettings.FallbackToLocalOnFailure)
            return false;

        return await _thumbService.GeneratedThumbExistsAsync(localThumbPath, thumbFileName);
    }

    private async Task<bool> UseLocalFallbackForExistsAsync(string thumbFileName, AliyunOssObjectExistsResult remoteExistsResult)
    {
        if (!_aliyunOssStorageSettings.FallbackToLocalOnFailure)
            return false;

        var localThumbPath = await _thumbService.GetThumbLocalPathByFileNameAsync(thumbFileName);
        if (!remoteExistsResult.Succeeded)
        {
            await _logger.WarningAsync(
                $"{AliyunOssStorageDefaults.SystemName}: Falling back to local thumbnail existence checks for '{thumbFileName}'. {remoteExistsResult.ErrorMessage}");
        }

        return await _thumbService.GeneratedThumbExistsAsync(localThumbPath, thumbFileName);
    }

    private async Task DeleteLocalThumbIfExistsAsync(string thumbFileName)
    {
        var localThumbPath = await _thumbService.GetThumbLocalPathByFileNameAsync(thumbFileName);
        if (_fileProvider.FileExists(localThumbPath))
            _fileProvider.DeleteFile(localThumbPath);
    }
}
