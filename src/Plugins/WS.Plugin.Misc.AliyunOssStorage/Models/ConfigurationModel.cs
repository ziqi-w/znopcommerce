using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace WS.Plugin.Misc.AliyunOssStorage.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".Endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".BucketName")]
    public string BucketName { get; set; } = string.Empty;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".Region")]
    public string Region { get; set; } = string.Empty;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".AccessKeyId")]
    public string AccessKeyId { get; set; } = string.Empty;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".AccessKeySecret")]
    [NoTrim]
    [DataType(DataType.Password)]
    public string AccessKeySecret { get; set; } = string.Empty;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".UseHttps")]
    public bool UseHttps { get; set; } = true;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".CustomBaseUrl")]
    public string CustomBaseUrl { get; set; } = string.Empty;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".BaseThumbPathPrefix")]
    public string BaseThumbPathPrefix { get; set; } = AliyunOssStorageDefaults.DefaultBaseThumbPathPrefix;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".DeleteLocalThumbAfterUpload")]
    public bool DeleteLocalThumbAfterUpload { get; set; }

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".FallbackToLocalOnFailure")]
    public bool FallbackToLocalOnFailure { get; set; } = true;

    [NopResourceDisplayName(AliyunOssStorageDefaults.LocaleResourcePrefix + ".Migration.BatchSize")]
    public int MigrationBatchSize { get; set; } = AliyunOssStorageDefaults.DefaultMigrationBatchSize;

    public bool HasStoredAccessKeySecret { get; set; }

    public bool IsTestConnectionRequested { get; set; }
}
