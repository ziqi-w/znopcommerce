using Microsoft.AspNetCore.Mvc;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using WS.Plugin.Misc.AliyunOssStorage.Models;
using WS.Plugin.Misc.AliyunOssStorage.Services;

namespace WS.Plugin.Misc.AliyunOssStorage.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class AliyunOssStorageController : BasePluginController
{
    private readonly IAliyunOssThumbMigrationService _aliyunOssThumbMigrationService;
    private readonly IAliyunOssStorageService _aliyunOssStorageService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly ISettingService _settingService;

    public AliyunOssStorageController(IAliyunOssThumbMigrationService aliyunOssThumbMigrationService,
        IAliyunOssStorageService aliyunOssStorageService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService)
    {
        _aliyunOssThumbMigrationService = aliyunOssThumbMigrationService;
        _aliyunOssStorageService = aliyunOssStorageService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure()
    {
        return View(AliyunOssStorageDefaults.ConfigureViewPath, await PrepareConfigurationModelAsync());
    }

    [HttpPost, ActionName("Configure")]
    [FormValueRequired("save")]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Save(ConfigurationModel model)
    {
        var currentSettings = await LoadCurrentSettingsAsync();

        if (!ModelState.IsValid)
            return View(AliyunOssStorageDefaults.ConfigureViewPath, PrepareConfigurationModel(model, currentSettings));

        var configuredSettings = BuildSettings(model, currentSettings);
        await _settingService.SaveSettingAsync(configuredSettings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    [HttpPost, ActionName("Configure")]
    [FormValueRequired("test-connection")]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> TestConnection(ConfigurationModel model)
    {
        model.IsTestConnectionRequested = true;
        var currentSettings = await LoadCurrentSettingsAsync();

        // Revalidate with the test flag so required connection fields are enforced without saving.
        ModelState.Clear();
        TryValidateModel(model);

        if (!ModelState.IsValid)
            return View(AliyunOssStorageDefaults.ConfigureViewPath, PrepareConfigurationModel(model, currentSettings));

        var testSettings = BuildSettings(model, currentSettings);
        var testResult = await _aliyunOssStorageService.TestConnectionAsync(testSettings);

        if (testResult.Succeeded)
        {
            var successMessage = await _localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection.Success");
            _notificationService.SuccessNotification(successMessage);
        }
        else
        {
            var failureMessageTemplate = await _localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection.Failed");
            var failureDetail = string.IsNullOrWhiteSpace(testResult.ErrorMessage)
                ? await _localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.TestConnection.Failed.Generic")
                : testResult.ErrorMessage;

            _notificationService.ErrorNotification(string.Format(failureMessageTemplate, failureDetail));
        }

        return View(AliyunOssStorageDefaults.ConfigureViewPath, PrepareConfigurationModel(model, currentSettings));
    }

    [HttpPost, ActionName("Configure")]
    [FormValueRequired("migrate-thumbnails")]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> MigrateThumbnails(ConfigurationModel model)
    {
        var currentSettings = await LoadCurrentSettingsAsync();
        ModelState.Clear();

        var batchSize = model.MigrationBatchSize <= 0
            ? AliyunOssStorageDefaults.DefaultMigrationBatchSize
            : model.MigrationBatchSize;

        if (batchSize > AliyunOssStorageDefaults.MaxMigrationBatchSize)
        {
            var message = string.Format(
                await _localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.BatchSize.Invalid"),
                AliyunOssStorageDefaults.MaxMigrationBatchSize);
            _notificationService.ErrorNotification(message);

            model.MigrationBatchSize = batchSize;
            return View(AliyunOssStorageDefaults.ConfigureViewPath, PrepareConfigurationModel(model, currentSettings));
        }

        // Migration intentionally uses the saved OSS settings so a bulk upload never runs against half-edited credentials.
        var result = await _aliyunOssThumbMigrationService.MigrateExistingThumbsAsync(
            new Services.Models.AliyunOssThumbMigrationRequest
            {
                BatchSize = batchSize
            },
            HttpContext?.RequestAborted ?? CancellationToken.None);

        if (result.Cancelled)
        {
            _notificationService.WarningNotification(await BuildMigrationSummaryMessageAsync(
                $"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Cancelled",
                result));
        }
        else if (!result.Succeeded)
        {
            var failureMessageTemplate = await _localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Failed");
            var failureDetail = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? await _localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Failed.Generic")
                : result.ErrorMessage;
            _notificationService.ErrorNotification(string.Format(failureMessageTemplate, failureDetail));
        }
        else if (result.Failed > 0)
        {
            _notificationService.WarningNotification(await BuildMigrationSummaryMessageAsync(
                $"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.CompletedWithFailures",
                result));
        }
        else
        {
            _notificationService.SuccessNotification(await BuildMigrationSummaryMessageAsync(
                $"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Migration.Completed",
                result));
        }

        model.MigrationBatchSize = batchSize;
        return View(AliyunOssStorageDefaults.ConfigureViewPath, PrepareConfigurationModel(model, currentSettings));
    }

    private async Task<ConfigurationModel> PrepareConfigurationModelAsync()
    {
        var settings = await LoadCurrentSettingsAsync();

        return PrepareConfigurationModel(new ConfigurationModel
        {
            Enabled = settings.Enabled,
            Endpoint = settings.Endpoint ?? string.Empty,
            BucketName = settings.BucketName ?? string.Empty,
            Region = settings.Region ?? string.Empty,
            AccessKeyId = settings.AccessKeyId ?? string.Empty,
            UseHttps = settings.UseHttps,
            CustomBaseUrl = settings.CustomBaseUrl ?? string.Empty,
            BaseThumbPathPrefix = string.IsNullOrWhiteSpace(settings.BaseThumbPathPrefix)
                ? AliyunOssStorageDefaults.DefaultBaseThumbPathPrefix
                : settings.BaseThumbPathPrefix,
            DeleteLocalThumbAfterUpload = settings.DeleteLocalThumbAfterUpload,
            FallbackToLocalOnFailure = settings.FallbackToLocalOnFailure,
            MigrationBatchSize = AliyunOssStorageDefaults.DefaultMigrationBatchSize
        }, settings);
    }

    private ConfigurationModel PrepareConfigurationModel(ConfigurationModel model, AliyunOssStorageSettings currentSettings)
    {
        model.AccessKeySecret = string.Empty;
        model.BaseThumbPathPrefix = string.IsNullOrWhiteSpace(model.BaseThumbPathPrefix)
            ? AliyunOssStorageDefaults.DefaultBaseThumbPathPrefix
            : model.BaseThumbPathPrefix;
        model.MigrationBatchSize = model.MigrationBatchSize <= 0
            ? AliyunOssStorageDefaults.DefaultMigrationBatchSize
            : model.MigrationBatchSize;
        model.HasStoredAccessKeySecret = !string.IsNullOrWhiteSpace(currentSettings.AccessKeySecret);
        model.IsTestConnectionRequested = false;

        return model;
    }

    private Task<AliyunOssStorageSettings> LoadCurrentSettingsAsync()
    {
        return _settingService.LoadSettingAsync<AliyunOssStorageSettings>();
    }

    private async Task<string> BuildMigrationSummaryMessageAsync(string resourceKey, Services.Models.AliyunOssThumbMigrationResult result)
    {
        var template = await _localizationService.GetResourceAsync(resourceKey);

        return string.Format(
            template,
            result.TotalScanned,
            result.Uploaded,
            result.Skipped,
            result.Failed);
    }

    private static AliyunOssStorageSettings BuildSettings(ConfigurationModel model, AliyunOssStorageSettings existingSettings = null)
    {
        return new AliyunOssStorageSettings
        {
            Enabled = model.Enabled,
            Endpoint = NormalizeText(model.Endpoint),
            BucketName = NormalizeText(model.BucketName),
            Region = NormalizeText(model.Region),
            AccessKeyId = NormalizeText(model.AccessKeyId),
            AccessKeySecret = !string.IsNullOrWhiteSpace(model.AccessKeySecret)
                ? model.AccessKeySecret.Trim()
                : NormalizeText(existingSettings?.AccessKeySecret),
            UseHttps = model.UseHttps,
            CustomBaseUrl = NormalizeBaseUrl(model.CustomBaseUrl),
            BaseThumbPathPrefix = NormalizeBaseThumbPathPrefix(model.BaseThumbPathPrefix),
            DeleteLocalThumbAfterUpload = model.DeleteLocalThumbAfterUpload,
            FallbackToLocalOnFailure = model.FallbackToLocalOnFailure
        };
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
    }

    private static string NormalizeBaseThumbPathPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return AliyunOssStorageDefaults.DefaultBaseThumbPathPrefix;

        var normalized = value.Trim().Replace('\\', '/');

        while (normalized.StartsWith('/'))
            normalized = normalized[1..];

        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(normalized))
            return AliyunOssStorageDefaults.DefaultBaseThumbPathPrefix;

        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : $"{normalized}/";
    }
}
