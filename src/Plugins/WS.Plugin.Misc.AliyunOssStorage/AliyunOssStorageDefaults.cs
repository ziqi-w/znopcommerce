using Nop.Core.Caching;

namespace WS.Plugin.Misc.AliyunOssStorage;

public static class AliyunOssStorageDefaults
{
    public const string SystemName = "Misc.AliyunOssStorage";
    public const string LocaleResourcePrefix = "WS.Plugin.Misc.AliyunOssStorage";
    public const string ConfigureViewPath = "~/Plugins/WS.Plugin.Misc.AliyunOssStorage/Views/Configure.cshtml";
    public const string DefaultBaseThumbPathPrefix = "thumbs/";
    public const int MaxEndpointLength = 512;
    public const int MaxBucketNameLength = 128;
    public const int MaxRegionLength = 100;
    public const int MaxAccessKeyIdLength = 256;
    public const int MaxCustomBaseUrlLength = 512;
    public const int MaxBaseThumbPathPrefixLength = 256;
    public const int DefaultMigrationBatchSize = 500;
    public const int MaxMigrationBatchSize = 10000;
    public const int MigrationProgressLogInterval = 100;

    public static CacheKey ThumbExistsCacheKey => new("WS.aliyun.thumb.exists.{0}");

    public static string ThumbsExistsPrefix => "WS.aliyun.thumb.exists.";
}
