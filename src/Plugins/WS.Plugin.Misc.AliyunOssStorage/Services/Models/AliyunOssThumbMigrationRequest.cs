namespace WS.Plugin.Misc.AliyunOssStorage.Services.Models;

public record AliyunOssThumbMigrationRequest
{
    public int BatchSize { get; init; } = AliyunOssStorageDefaults.DefaultMigrationBatchSize;
}
