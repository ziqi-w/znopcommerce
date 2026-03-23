using Microsoft.AspNetCore.Mvc;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain.Enums;
using WS.Plugin.Misc.GoogleMerchantCenter.Factories;
using WS.Plugin.Misc.GoogleMerchantCenter.Models.Admin;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Controllers;

[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public class GoogleMerchantCenterController : BasePluginController
{
    private readonly IGoogleMerchantFeedSnapshotService _feedSnapshotService;
    private readonly IGoogleMerchantScheduleTaskService _googleMerchantScheduleTaskService;
    private readonly ILocalizationService _localizationService;
    private readonly GoogleMerchantCenterModelFactory _modelFactory;
    private readonly INotificationService _notificationService;
    private readonly ISettingService _settingService;

    public GoogleMerchantCenterController(IGoogleMerchantFeedSnapshotService feedSnapshotService,
        IGoogleMerchantScheduleTaskService googleMerchantScheduleTaskService,
        ILocalizationService localizationService,
        GoogleMerchantCenterModelFactory modelFactory,
        INotificationService notificationService,
        ISettingService settingService)
    {
        _feedSnapshotService = feedSnapshotService;
        _googleMerchantScheduleTaskService = googleMerchantScheduleTaskService;
        _localizationService = localizationService;
        _modelFactory = modelFactory;
        _notificationService = notificationService;
        _settingService = settingService;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure()
    {
        await _googleMerchantScheduleTaskService.EnsureTaskAsync();
        var model = await _modelFactory.PrepareConfigurationModelAsync();
        return View(GoogleMerchantCenterDefaults.ConfigureViewPath, model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
        {
            model = await _modelFactory.PrepareConfigurationModelAsync(model);
            return View(GoogleMerchantCenterDefaults.ConfigureViewPath, model);
        }

        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();

        settings.Enabled = model.Enabled;
        settings.FeedToken = NormalizeText(model.FeedToken);
        settings.FeedFormat = GoogleMerchantFeedFormat.Xml;
        settings.DefaultCurrencyCode = NormalizeCode(model.DefaultCurrencyCode);
        settings.DefaultCountryCode = NormalizeCode(model.DefaultCountryCode);
        settings.IncludeUnpublishedProducts = model.IncludeUnpublishedProducts;
        settings.IncludeProductsWithoutImages = model.IncludeProductsWithoutImages;
        settings.IncludeOutOfStockProducts = model.IncludeOutOfStockProducts;
        settings.LimitedToStoreIdsCsv = SerializeStoreIds(model.SelectedStoreIds);
        settings.IncludeShipping = model.IncludeShipping;
        settings.DefaultShippingCountryCode = NormalizeCode(model.DefaultShippingCountryCode);
        settings.DefaultShippingService = NormalizeText(model.DefaultShippingService);
        settings.DefaultShippingPrice = model.DefaultShippingPrice;
        settings.BrandFallbackStrategy = model.BrandFallbackStrategy;
        settings.GtinFallbackStrategy = model.GtinFallbackStrategy;
        settings.MpnFallbackStrategy = model.MpnFallbackStrategy;
        settings.DefaultCondition = model.DefaultCondition;
        settings.ExportAdditionalImageLinks = model.ExportAdditionalImageLinks;
        settings.FeedRegenerationIntervalMinutes = model.FeedRegenerationIntervalMinutes;

        await _settingService.SaveSettingAsync(settings);
        await _feedSnapshotService.InvalidateSnapshotsAsync();
        await _googleMerchantScheduleTaskService.EnsureTaskAsync();
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction(nameof(Configure));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> GenerateNow()
    {
        try
        {
            var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();

            var result = await _feedSnapshotService.RegenerateAsync(new GoogleMerchantFeedRequest
            {
                ForceRegeneration = true,
                CountryCode = settings.DefaultCountryCode,
                CurrencyCode = settings.DefaultCurrencyCode
            });

            if (result.Succeeded)
            {
                _notificationService.SuccessNotification(string.IsNullOrWhiteSpace(result.Diagnostics.Summary)
                    ? await _localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Notifications.GenerateNowSucceeded")
                    : result.Diagnostics.Summary);
            }
            else
            {
                _notificationService.ErrorNotification(string.IsNullOrWhiteSpace(result.Diagnostics.Summary)
                    ? await _localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Notifications.GenerateNowFailed")
                    : result.Diagnostics.Summary);
            }
        }
        catch
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Notifications.GenerateNowFailed"));
        }

        return RedirectToAction(nameof(Configure));
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static string SerializeStoreIds(IEnumerable<int> storeIds)
    {
        if (storeIds is null)
            return null;

        var distinctStoreIds = storeIds
            .Where(storeId => storeId > 0)
            .Distinct()
            .OrderBy(storeId => storeId)
            .ToArray();

        return distinctStoreIds.Length == 0 ? null : string.Join(',', distinctStoreIds);
    }
}
