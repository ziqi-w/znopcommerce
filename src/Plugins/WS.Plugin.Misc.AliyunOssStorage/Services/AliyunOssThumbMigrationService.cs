using Nop.Core.Caching;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Services.Logging;
using Nop.Services.Media;
using WS.Plugin.Misc.AliyunOssStorage.Services.Models;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public class AliyunOssThumbMigrationService : IAliyunOssThumbMigrationService
{
    private readonly AliyunOssStorageSettings _aliyunOssStorageSettings;
    private readonly INopFileProvider _fileProvider;
    private readonly IAliyunOssStorageService _aliyunOssStorageService;
    private readonly ILogger _logger;
    private readonly MediaSettings _mediaSettings;
    private readonly IStaticCacheManager _staticCacheManager;

    public AliyunOssThumbMigrationService(AliyunOssStorageSettings aliyunOssStorageSettings,
        INopFileProvider fileProvider,
        IAliyunOssStorageService aliyunOssStorageService,
        ILogger logger,
        MediaSettings mediaSettings,
        IStaticCacheManager staticCacheManager)
    {
        _aliyunOssStorageSettings = aliyunOssStorageSettings;
        _fileProvider = fileProvider;
        _aliyunOssStorageService = aliyunOssStorageService;
        _logger = logger;
        _mediaSettings = mediaSettings;
        _staticCacheManager = staticCacheManager;
    }

    public async Task<AliyunOssThumbMigrationResult> MigrateExistingThumbsAsync(AliyunOssThumbMigrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batchSize = request.BatchSize <= 0
            ? AliyunOssStorageDefaults.DefaultMigrationBatchSize
            : Math.Min(request.BatchSize, AliyunOssStorageDefaults.MaxMigrationBatchSize);

        var connectionResult = await _aliyunOssStorageService.TestConnectionAsync(cancellationToken);
        if (!connectionResult.Succeeded)
            return AliyunOssThumbMigrationResult.Failure(connectionResult.ErrorMessage ?? "The OSS connection test failed.");

        var totalScanned = 0;
        var uploaded = 0;
        var skipped = 0;
        var failed = 0;
        var localDeleted = 0;

        try
        {
            var thumbsPath = _fileProvider.Combine(_fileProvider.GetLocalImagesPath(_mediaSettings), NopMediaDefaults.ImageThumbsPath);
            if (!_fileProvider.DirectoryExists(thumbsPath))
            {
                const string message = "No local thumbnail directory was found to migrate.";
                await _logger.InformationAsync($"{AliyunOssStorageDefaults.SystemName}: {message}");
                return AliyunOssThumbMigrationResult.Success(0, 0, 0, 0, 0);
            }

            var thumbFiles = _fileProvider.GetFiles(thumbsPath, topDirectoryOnly: false)
                .Where(_fileProvider.FileExists)
                .Where(path => !_fileProvider.GetFileName(path).Equals("placeholder.txt", StringComparison.OrdinalIgnoreCase))
                .Take(batchSize)
                .ToList();

            await _logger.InformationAsync(
                $"{AliyunOssStorageDefaults.SystemName}: Starting thumbnail migration scan for bucket '{_aliyunOssStorageSettings.BucketName}'. " +
                $"Batch size {batchSize}, local delete after upload: {_aliyunOssStorageSettings.DeleteLocalThumbAfterUpload}.");

            foreach (var localThumbPath in thumbFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalScanned++;

                var thumbFileName = _fileProvider.GetFileName(localThumbPath);
                var objectKey = _aliyunOssStorageService.BuildObjectKey(thumbFileName);

                try
                {
                    // Deterministic thumb keys make reruns idempotent: if the remote object is already there, skip it.
                    var existsResult = await _aliyunOssStorageService.ObjectExistsAsync(objectKey, cancellationToken);
                    if (!existsResult.Succeeded)
                    {
                        failed++;
                        await _logger.WarningAsync(
                            $"{AliyunOssStorageDefaults.SystemName}: Failed to check whether thumbnail '{thumbFileName}' already exists in OSS. {existsResult.ErrorMessage}");
                        continue;
                    }

                    if (existsResult.Exists)
                    {
                        skipped++;
                        continue;
                    }

                    await using var stream = new FileStream(localThumbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var uploadResult = await _aliyunOssStorageService.UploadObjectAsync(
                        objectKey,
                        stream,
                        _aliyunOssStorageService.ResolveContentType(thumbFileName),
                        cancellationToken);

                    if (!uploadResult.Succeeded)
                    {
                        failed++;
                        await _logger.WarningAsync(
                            $"{AliyunOssStorageDefaults.SystemName}: Failed to migrate local thumbnail '{thumbFileName}' to OSS. {uploadResult.ErrorMessage}");
                        continue;
                    }

                    uploaded++;

                    // Only remove the local copy after the current migration run has confirmed a successful OSS upload.
                    if (_aliyunOssStorageSettings.DeleteLocalThumbAfterUpload &&
                        await DeleteLocalThumbAsync(localThumbPath, thumbFileName))
                    {
                        localDeleted++;
                    }

                    if (totalScanned % AliyunOssStorageDefaults.MigrationProgressLogInterval == 0)
                    {
                        await _logger.InformationAsync(
                            $"{AliyunOssStorageDefaults.SystemName}: Thumbnail migration progress. " +
                            $"Scanned {totalScanned}, uploaded {uploaded}, skipped {skipped}, failed {failed}.");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failed++;
                    await _logger.ErrorAsync(
                        $"{AliyunOssStorageDefaults.SystemName}: Unexpected error while migrating local thumbnail '{thumbFileName}'.",
                        exception);
                }
            }
            if (uploaded > 0)
                await _staticCacheManager.RemoveByPrefixAsync(AliyunOssStorageDefaults.ThumbsExistsPrefix);

            var summaryMessage = $"{AliyunOssStorageDefaults.SystemName}: Thumbnail migration completed. " +
                                 $"Scanned {totalScanned}, uploaded {uploaded}, skipped {skipped}, failed {failed}, local deleted {localDeleted}.";

            if (failed > 0)
                await _logger.WarningAsync(summaryMessage);
            else
                await _logger.InformationAsync(summaryMessage);

            return AliyunOssThumbMigrationResult.Success(totalScanned, uploaded, skipped, failed, localDeleted);
        }
        catch (OperationCanceledException)
        {
            await _logger.WarningAsync(
                $"{AliyunOssStorageDefaults.SystemName}: Thumbnail migration was cancelled after scanning {totalScanned} files.");
            return AliyunOssThumbMigrationResult.CancelledResult(totalScanned, uploaded, skipped, failed, localDeleted);
        }
        catch (Exception exception)
        {
            const string errorMessage = "Unexpected error while scanning local thumbnails for migration.";
            await _logger.ErrorAsync($"{AliyunOssStorageDefaults.SystemName}: {errorMessage}", exception);
            return AliyunOssThumbMigrationResult.Failure(errorMessage, totalScanned, uploaded, skipped, failed, localDeleted);
        }
    }

    private async Task<bool> DeleteLocalThumbAsync(string localThumbPath, string thumbFileName)
    {
        try
        {
            if (!_fileProvider.FileExists(localThumbPath))
                return false;

            _fileProvider.DeleteFile(localThumbPath);
            return true;
        }
        catch (Exception exception)
        {
            await _logger.WarningAsync(
                $"{AliyunOssStorageDefaults.SystemName}: OSS upload succeeded for thumbnail '{thumbFileName}', but deleting the local thumb file failed.",
                exception);
            return false;
        }
    }
}
