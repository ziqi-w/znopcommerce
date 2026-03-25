namespace WS.Plugin.Misc.AliyunOssStorage.Services.Models;

public record AliyunOssThumbMigrationResult
{
    public bool Succeeded { get; init; }

    public bool Cancelled { get; init; }

    public int TotalScanned { get; init; }

    public int Uploaded { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    public int LocalDeleted { get; init; }

    public string ErrorMessage { get; init; }

    public static AliyunOssThumbMigrationResult Success(int totalScanned, int uploaded, int skipped, int failed, int localDeleted)
    {
        return new()
        {
            Succeeded = true,
            TotalScanned = totalScanned,
            Uploaded = uploaded,
            Skipped = skipped,
            Failed = failed,
            LocalDeleted = localDeleted
        };
    }

    public static AliyunOssThumbMigrationResult Failure(string errorMessage, int totalScanned = 0, int uploaded = 0, int skipped = 0, int failed = 0, int localDeleted = 0)
    {
        return new()
        {
            Succeeded = false,
            ErrorMessage = errorMessage,
            TotalScanned = totalScanned,
            Uploaded = uploaded,
            Skipped = skipped,
            Failed = failed,
            LocalDeleted = localDeleted
        };
    }

    public static AliyunOssThumbMigrationResult CancelledResult(int totalScanned, int uploaded, int skipped, int failed, int localDeleted)
    {
        return new()
        {
            Succeeded = false,
            Cancelled = true,
            ErrorMessage = "The thumbnail migration was cancelled before completion.",
            TotalScanned = totalScanned,
            Uploaded = uploaded,
            Skipped = skipped,
            Failed = failed,
            LocalDeleted = localDeleted
        };
    }
}
