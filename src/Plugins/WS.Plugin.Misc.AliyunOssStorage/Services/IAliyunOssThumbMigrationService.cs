using WS.Plugin.Misc.AliyunOssStorage.Services.Models;

namespace WS.Plugin.Misc.AliyunOssStorage.Services;

public interface IAliyunOssThumbMigrationService
{
    Task<AliyunOssThumbMigrationResult> MigrateExistingThumbsAsync(AliyunOssThumbMigrationRequest request, CancellationToken cancellationToken = default);
}
