using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Web.Framework.Mvc.Routing;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain.Enums;

namespace WS.Plugin.Misc.GoogleMerchantCenter;

public class WSGoogleMerchantCenterPlugin : BasePlugin, IMiscPlugin
{
    private readonly ILocalizationService _localizationService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly ISettingService _settingService;

    public WSGoogleMerchantCenterPlugin(ILocalizationService localizationService,
        INopUrlHelper nopUrlHelper,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService)
    {
        _localizationService = localizationService;
        _nopUrlHelper = nopUrlHelper;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
    }

    public override string GetConfigurationPageUrl()
    {
        return _nopUrlHelper.RouteUrl(GoogleMerchantCenterDefaults.ConfigurationRouteName);
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new GoogleMerchantCenterSettings
        {
            Enabled = false,
            FeedToken = Guid.NewGuid().ToString("N"),
            FeedFormat = GoogleMerchantFeedFormat.Xml,
            DefaultCurrencyCode = GoogleMerchantCenterDefaults.DefaultCurrencyCode,
            DefaultCountryCode = GoogleMerchantCenterDefaults.DefaultCountryCode,
            IncludeUnpublishedProducts = false,
            IncludeProductsWithoutImages = false,
            IncludeOutOfStockProducts = false,
            LimitedToStoreIdsCsv = null,
            IncludeShipping = false,
            DefaultShippingCountryCode = GoogleMerchantCenterDefaults.DefaultCountryCode,
            DefaultShippingService = "Standard",
            DefaultShippingPrice = 0m,
            BrandFallbackStrategy = GoogleMerchantBrandFallbackStrategy.None,
            GtinFallbackStrategy = GoogleMerchantGtinFallbackStrategy.None,
            MpnFallbackStrategy = GoogleMerchantMpnFallbackStrategy.None,
            DefaultCondition = GoogleMerchantProductCondition.New,
            ExportAdditionalImageLinks = true,
            FeedRegenerationIntervalMinutes = GoogleMerchantCenterDefaults.DefaultFeedRegenerationIntervalMinutes,
            LastGenerationUtc = null,
            LastGenerationStatus = "NotGenerated",
            LastGeneratedItemCount = 0,
            LastSkippedItemCount = 0,
            LastWarningCount = 0,
            LastErrorCount = 0,
            LastGenerationSummary = "The feed has not been generated yet.",
            LastGenerationMessagesJson = null
        });

        var scheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.ScheduleTaskType)
            ?? await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.LegacyScheduleTaskType);

        if (scheduleTask is null)
        {
            await _scheduleTaskService.InsertTaskAsync(new Nop.Core.Domain.ScheduleTasks.ScheduleTask
            {
                Name = GoogleMerchantCenterDefaults.ScheduleTaskName,
                Type = GoogleMerchantCenterDefaults.ScheduleTaskType,
                Enabled = false,
                Seconds = GoogleMerchantCenterDefaults.DefaultFeedRegenerationIntervalMinutes * 60,
                StopOnError = false
            });
        }

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.General"] = "General",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.Selection"] = "Product selection",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.Attributes"] = "Attribute mapping",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.Shipping"] = "Shipping and feed URLs",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.Diagnostics"] = "Diagnostics",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.Instructions"] = "Configure the Google Merchant Center product feed endpoint, conservative export rules, and feed regeneration settings.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Configuration.StoreRestrictionHelp"] = "Leave the store list empty to allow all stores. Select one or more stores to keep the feed scoped to those storefronts only.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Actions.GenerateNow"] = "Generate feed now",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Notifications.GenerateNowSucceeded"] = "The Google Merchant Center feed generated successfully.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Notifications.GenerateNowFailed"] = "The Google Merchant Center feed could not be generated.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.Enabled"] = "Enabled",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.Enabled.Hint"] = "Enable the plugin and allow the protected feed endpoint to serve content.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedToken"] = "Feed token",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedToken.Hint"] = "Secret token required as a query parameter when Google fetches the feed.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedToken.Required"] = "A feed token is required.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedToken.Length"] = $"The feed token cannot exceed {GoogleMerchantCenterDefaults.MaxFeedTokenLength} characters.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedFormat"] = "Feed format",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedFormat.Hint"] = "The plugin currently exports XML feeds for Google Merchant Center fetches.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedFormat.Xml"] = "XML",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedFormat.TabDelimited"] = "Tab-delimited",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCurrencyCode"] = "Default currency code",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCurrencyCode.Hint"] = "Currency code used when no explicit feed currency parameter is supplied.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCurrencyCode.Required"] = "A default currency code is required.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCurrencyCode.Invalid"] = "Enter a valid 3-letter ISO currency code such as NZD.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCountryCode"] = "Default country code",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCountryCode.Hint"] = "Country code used for Google Merchant Center targeting and optional shipping defaults.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCountryCode.Required"] = "A default country code is required.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCountryCode.Invalid"] = "Enter a valid 2-letter ISO country code such as NZ.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeUnpublishedProducts"] = "Include unpublished products",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeUnpublishedProducts.Hint"] = "Off by default. Google-facing feeds should normally exclude unpublished catalog items.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeProductsWithoutImages"] = "Include products without images",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeProductsWithoutImages.Hint"] = "Off by default. Google shopping listings typically require a usable primary image.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeOutOfStockProducts"] = "Include out-of-stock products",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeOutOfStockProducts.Hint"] = "Allow the feed to include items that are unavailable or backorderable, subject to later availability mapping rules.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.SelectedStoreIds"] = "Limited stores",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.SelectedStoreIds.Hint"] = "Choose the stores that the feed is allowed to export from.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeShipping"] = "Include default shipping block",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.IncludeShipping.Hint"] = "Enable placeholder shipping settings so the feed can emit a default Google shipping node when required.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingCountryCode"] = "Shipping country code",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingCountryCode.Hint"] = "Default country code used in the shipping node when shipping export is enabled.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingCountryCode.Required"] = "A shipping country code is required when shipping export is enabled.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingCountryCode.Invalid"] = "Enter a valid 2-letter ISO country code for shipping.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingService"] = "Shipping service",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingService.Hint"] = "Human-readable service name for the default shipping node.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingService.Required"] = "A shipping service name is required when shipping export is enabled.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingService.Length"] = $"The shipping service name cannot exceed {GoogleMerchantCenterDefaults.MaxShippingServiceLength} characters.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingPrice"] = "Shipping price",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingPrice.Hint"] = "Default shipping price used in the placeholder shipping node.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingPrice.Range"] = "Shipping price must be zero or greater.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy"] = "Brand fallback strategy",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.Hint"] = "Conservative source preference used only when a direct brand mapping is unavailable.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.None"] = "Do not fallback",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.Manufacturer"] = "Use manufacturer name",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.Vendor"] = "Use vendor name",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.StoreName"] = "Use store name",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.GtinFallbackStrategy"] = "GTIN fallback strategy",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.GtinFallbackStrategy.Hint"] = "Reserved extension point for selecting a non-fabricated GTIN source when the default mapping is empty.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.GtinFallbackStrategy.None"] = "Do not fallback",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.GtinFallbackStrategy.GtinAttribute"] = "Use GTIN attribute",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.MpnFallbackStrategy"] = "MPN fallback strategy",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.MpnFallbackStrategy.Hint"] = "Reserved extension point for selecting a non-fabricated MPN source when the default mapping is empty.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.MpnFallbackStrategy.None"] = "Do not fallback",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.MpnFallbackStrategy.MpnAttribute"] = "Use MPN attribute",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition"] = "Default condition",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.Hint"] = "Fallback item condition used when a product-level condition is not mapped later.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.New"] = "New",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.Refurbished"] = "Refurbished",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.Used"] = "Used",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.ExportAdditionalImageLinks"] = "Export additional image links",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.ExportAdditionalImageLinks.Hint"] = "Enable the future export of secondary product images when valid images are available.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedRegenerationIntervalMinutes"] = "Feed regeneration interval (minutes)",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedRegenerationIntervalMinutes.Hint"] = "Controls how long the public endpoint serves the cached feed snapshot before regenerating it.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedRegenerationIntervalMinutes.Range"] = $"Enter a value between {GoogleMerchantCenterDefaults.MinFeedRegenerationIntervalMinutes} and {GoogleMerchantCenterDefaults.MaxFeedRegenerationIntervalMinutes} minutes.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedUrl"] = "Primary feed URL",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedUrl.Hint"] = "Preferred stable endpoint for Google Merchant Center fetches.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.PluginFeedUrl"] = "Alternate plugin feed URL",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.PluginFeedUrl.Hint"] = "Alternate endpoint routed through the plugin path for environments that prefer plugin-specific URLs.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGenerationUtc"] = "Last generation time (UTC)",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGenerationUtc.Hint"] = "UTC timestamp of the latest feed generation attempt recorded by the plugin.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGenerationStatus"] = "Last generation status",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGenerationStatus.Hint"] = "High-level status from the most recent feed generation attempt.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGeneratedItemCount"] = "Last generated item count",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGeneratedItemCount.Hint"] = "Number of feed items emitted during the latest generation attempt.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastSkippedItemCount"] = "Last skipped item count",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastSkippedItemCount.Hint"] = "Number of candidate items skipped during the latest generation attempt.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastWarningCount"] = "Last warning count",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastWarningCount.Hint"] = "Number of warnings captured during the latest generation attempt.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastErrorCount"] = "Last error count",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastErrorCount.Hint"] = "Number of errors captured during the latest generation attempt.",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGenerationSummary"] = "Last generation summary",
            [$"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.LastGenerationSummary.Hint"] = "Short diagnostics summary persisted from the most recent feed generation attempt."
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<GoogleMerchantCenterSettings>();
        await _localizationService.DeleteLocaleResourcesAsync(GoogleMerchantCenterDefaults.LocaleResourcePrefix);

        var scheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.ScheduleTaskType);
        if (scheduleTask is not null)
            await _scheduleTaskService.DeleteTaskAsync(scheduleTask);

        var legacyScheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.LegacyScheduleTaskType);
        if (legacyScheduleTask is not null)
            await _scheduleTaskService.DeleteTaskAsync(legacyScheduleTask);

        await base.UninstallAsync();
    }
}
