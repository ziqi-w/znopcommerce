using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace WS.Plugin.Misc.AliyunOssStorage;

public class AliyunOssStoragePlugin : BasePlugin, IMiscPlugin
{
    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    public AliyunOssStoragePlugin(ILocalizationService localizationService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/AliyunOssStorage/Configure";
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new AliyunOssStorageSettings
        {
            Enabled = false,
            Endpoint = string.Empty,
            BucketName = string.Empty,
            Region = string.Empty,
            AccessKeyId = string.Empty,
            AccessKeySecret = string.Empty,
            UseHttps = true,
            CustomBaseUrl = string.Empty,
            BaseThumbPathPrefix = AliyunOssStorageDefaults.DefaultBaseThumbPathPrefix,
            DeleteLocalThumbAfterUpload = false,
            FallbackToLocalOnFailure = true
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Title"] = "Aliyun OSS thumbnail storage configuration",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Instructions"] = "Configure Alibaba Cloud OSS for generated nopCommerce thumbnails. Save the settings to enable remote thumbnail storage, or use Test Connection first to verify access.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Enabled"] = "Enabled",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Enabled.Hint"] = "Enable this setting to route generated picture thumbnails through the Aliyun OSS thumbnail provider.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Endpoint"] = "Endpoint",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Endpoint.Hint"] = "Specify the OSS endpoint host name for the target bucket, for example oss-ap-southeast-2.aliyuncs.com.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BucketName"] = "Bucket name",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BucketName.Hint"] = "Specify the OSS bucket that should store generated thumbnails.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Region"] = "Region",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Region.Hint"] = "Specify the Aliyun region identifier used by the bucket, for example ap-southeast-2.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeyId"] = "Access key ID",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeyId.Hint"] = "Specify the access key ID used for OSS API operations.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeySecret"] = "Access key secret",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeySecret.Hint"] = "Specify the access key secret used for OSS API operations.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeySecret.KeepExisting"] = "Leave this field blank to keep the currently stored secret.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.UseHttps"] = "Use HTTPS",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.UseHttps.Hint"] = "Use HTTPS when constructing public thumbnail URLs if a custom base URL is not supplied.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.CustomBaseUrl"] = "Custom base URL",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.CustomBaseUrl.Hint"] = "Optional CDN or custom domain base URL for published thumbnails. When specified, it takes precedence over the bucket-derived URL.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BaseThumbPathPrefix"] = "Base thumb path prefix",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BaseThumbPathPrefix.Hint"] = "Optional object key prefix for generated thumbnails. The default value is thumbs/.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.DeleteLocalThumbAfterUpload"] = "Delete local thumb after upload",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.DeleteLocalThumbAfterUpload.Hint"] = "Delete any local thumb file that still exists after a successful OSS upload.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.FallbackToLocalOnFailure"] = "Fallback to local on failure",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.FallbackToLocalOnFailure.Hint"] = "When enabled, thumbnail operations can safely fall back to local storage if the OSS provider is unavailable.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Warning.ThumbsOnly"] = "This plugin stores generated picture thumbnails only.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Warning.OriginalImages"] = "Original images remain in nopCommerce built-in picture storage.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Help.CustomBaseUrlOptional"] = "Custom base URL is optional. Leave it blank to publish thumbnails by using the bucket and endpoint URL pattern.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Help.LocalFallback"] = "If local fallback is enabled, storefront thumbnail requests can continue to use local thumb files when OSS operations fail.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection"] = "Test connection",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection.Success"] = "Aliyun OSS connection test succeeded.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection.Failed"] = "Aliyun OSS connection test failed: {0}",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection.Failed.Generic"] = "The OSS connection test failed for an unspecified reason.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Title"] = "Migrate existing thumbnails",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Description"] = "Scan the current local thumbnail store and upload thumbnails that are missing in OSS. This operation only touches generated thumbnails.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.UsesSavedSettings"] = "Migration uses the currently saved OSS settings. Save configuration changes before starting a migration run.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.SafeDeleteNotice"] = "Local thumbnail files are only deleted when Delete local thumb after upload is enabled and the upload succeeds in the current migration run.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.BatchSize"] = "Batch size",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.BatchSize.Hint"] = "Maximum number of local thumbnails to scan in one migration run. Use smaller batches for safer staged migration.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.BatchSize.Invalid"] = "Batch size must be between 1 and {0}.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Run"] = "Migrate thumbnails",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Completed"] = "Thumbnail migration completed. Scanned {0}, uploaded {1}, skipped {2}, failed {3}.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.CompletedWithFailures"] = "Thumbnail migration completed with issues. Scanned {0}, uploaded {1}, skipped {2}, failed {3}.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Cancelled"] = "Thumbnail migration was cancelled. Scanned {0}, uploaded {1}, skipped {2}, failed {3}.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Failed"] = "Thumbnail migration could not start: {0}",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Failed.Generic"] = "The migration could not start because the OSS connection or saved settings are not valid.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Endpoint.Required"] = "The endpoint is required when the plugin is enabled or when testing the connection.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Endpoint.Length"] = $"The endpoint cannot exceed {AliyunOssStorageDefaults.MaxEndpointLength} characters.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BucketName.Required"] = "The bucket name is required when the plugin is enabled or when testing the connection.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BucketName.Length"] = $"The bucket name cannot exceed {AliyunOssStorageDefaults.MaxBucketNameLength} characters.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Region.Required"] = "The region is required when the plugin is enabled or when testing the connection.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Region.Length"] = $"The region cannot exceed {AliyunOssStorageDefaults.MaxRegionLength} characters.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeyId.Required"] = "The access key ID is required when the plugin is enabled or when testing the connection.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeyId.Length"] = $"The access key ID cannot exceed {AliyunOssStorageDefaults.MaxAccessKeyIdLength} characters.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeySecret.Required"] = "The access key secret is required when the plugin is enabled or when testing the connection.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.CustomBaseUrl.Invalid"] = "Enter a valid absolute HTTP or HTTPS URL for the custom base URL.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.CustomBaseUrl.Length"] = $"The custom base URL cannot exceed {AliyunOssStorageDefaults.MaxCustomBaseUrlLength} characters.",
            [$"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BaseThumbPathPrefix.Length"] = $"The base thumb path prefix cannot exceed {AliyunOssStorageDefaults.MaxBaseThumbPathPrefixLength} characters."
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<AliyunOssStorageSettings>();
        await _localizationService.DeleteLocaleResourcesAsync(AliyunOssStorageDefaults.LocaleResourcePrefix);

        await base.UninstallAsync();
    }
}
