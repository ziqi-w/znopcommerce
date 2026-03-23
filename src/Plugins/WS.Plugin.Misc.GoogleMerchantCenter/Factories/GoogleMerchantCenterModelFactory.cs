using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework.Mvc.Routing;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain.Enums;
using WS.Plugin.Misc.GoogleMerchantCenter.Models.Admin;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Factories;

public class GoogleMerchantCenterModelFactory
{
    private readonly IBaseAdminModelFactory _baseAdminModelFactory;
    private readonly IGoogleMerchantDiagnosticsService _diagnosticsService;
    private readonly ILocalizationService _localizationService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    public GoogleMerchantCenterModelFactory(IBaseAdminModelFactory baseAdminModelFactory,
        IGoogleMerchantDiagnosticsService diagnosticsService,
        ILocalizationService localizationService,
        INopUrlHelper nopUrlHelper,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _baseAdminModelFactory = baseAdminModelFactory;
        _diagnosticsService = diagnosticsService;
        _localizationService = localizationService;
        _nopUrlHelper = nopUrlHelper;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    public async Task<ConfigurationModel> PrepareConfigurationModelAsync()
    {
        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();

        var model = new ConfigurationModel
        {
            Enabled = settings.Enabled,
            FeedToken = settings.FeedToken,
            FeedFormat = GoogleMerchantFeedFormat.Xml,
            DefaultCurrencyCode = settings.DefaultCurrencyCode,
            DefaultCountryCode = settings.DefaultCountryCode,
            IncludeUnpublishedProducts = settings.IncludeUnpublishedProducts,
            IncludeProductsWithoutImages = settings.IncludeProductsWithoutImages,
            IncludeOutOfStockProducts = settings.IncludeOutOfStockProducts,
            IncludeShipping = settings.IncludeShipping,
            DefaultShippingCountryCode = settings.DefaultShippingCountryCode,
            DefaultShippingService = settings.DefaultShippingService,
            DefaultShippingPrice = settings.DefaultShippingPrice,
            BrandFallbackStrategy = settings.BrandFallbackStrategy,
            GtinFallbackStrategy = settings.GtinFallbackStrategy,
            MpnFallbackStrategy = settings.MpnFallbackStrategy,
            DefaultCondition = settings.DefaultCondition,
            ExportAdditionalImageLinks = settings.ExportAdditionalImageLinks,
            FeedRegenerationIntervalMinutes = settings.FeedRegenerationIntervalMinutes,
            SelectedStoreIds = ParseStoreIds(settings.LimitedToStoreIdsCsv)
        };

        await PrepareSharedModelValuesAsync(model, settings);

        return model;
    }

    public async Task<ConfigurationModel> PrepareConfigurationModelAsync(ConfigurationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();
        await PrepareSharedModelValuesAsync(model, settings);

        return model;
    }

    private async Task PrepareSharedModelValuesAsync(ConfigurationModel model, GoogleMerchantCenterSettings settings)
    {
        await PrepareSelectListsAsync(model);
        await _baseAdminModelFactory.PrepareStoresAsync(model.AvailableStores, false);

        var diagnostics = await _diagnosticsService.GetLastSummaryAsync();
        model.LastGenerationUtc = diagnostics.GeneratedOnUtc;
        model.LastGenerationStatus = diagnostics.Status;
        model.LastGeneratedItemCount = diagnostics.ExportedItemCount;
        model.LastSkippedItemCount = diagnostics.SkippedItemCount;
        model.LastWarningCount = diagnostics.WarningCount;
        model.LastErrorCount = diagnostics.ErrorCount;
        model.LastGenerationSummary = diagnostics.Summary;
        model.LastGenerationMessages = diagnostics.Messages
            .Select(message => new ConfigurationDiagnosticMessageModel
            {
                Severity = message.Severity.ToString(),
                Code = message.Code,
                ProductId = message.ProductId,
                Message = message.Message
            })
            .ToList();
        model.FeedUrl = BuildAbsoluteRouteUrl(GoogleMerchantCenterDefaults.FeedRouteName, new Dictionary<string, object>
        {
            [GoogleMerchantCenterDefaults.TokenQueryParameterName] = settings.FeedToken
        });
        model.PluginFeedUrl = BuildAbsoluteRouteUrl(GoogleMerchantCenterDefaults.PluginFeedRouteName, new Dictionary<string, object>
        {
            [GoogleMerchantCenterDefaults.TokenQueryParameterName] = settings.FeedToken
        });
    }

    private async Task PrepareSelectListsAsync(ConfigurationModel model)
    {
        model.AvailableFeedFormats = await BuildSelectListAsync(model.FeedFormat, new Dictionary<GoogleMerchantFeedFormat, string>
        {
            [GoogleMerchantFeedFormat.Xml] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedFormat.Xml"
        });

        model.AvailableBrandFallbackStrategies = await BuildSelectListAsync(model.BrandFallbackStrategy, new Dictionary<GoogleMerchantBrandFallbackStrategy, string>
        {
            [GoogleMerchantBrandFallbackStrategy.None] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.None",
            [GoogleMerchantBrandFallbackStrategy.Manufacturer] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.Manufacturer",
            [GoogleMerchantBrandFallbackStrategy.Vendor] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.Vendor",
            [GoogleMerchantBrandFallbackStrategy.StoreName] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.BrandFallbackStrategy.StoreName"
        });

        model.AvailableGtinFallbackStrategies = await BuildSelectListAsync(model.GtinFallbackStrategy, new Dictionary<GoogleMerchantGtinFallbackStrategy, string>
        {
            [GoogleMerchantGtinFallbackStrategy.None] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.GtinFallbackStrategy.None",
            [GoogleMerchantGtinFallbackStrategy.GtinAttribute] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.GtinFallbackStrategy.GtinAttribute"
        });

        model.AvailableMpnFallbackStrategies = await BuildSelectListAsync(model.MpnFallbackStrategy, new Dictionary<GoogleMerchantMpnFallbackStrategy, string>
        {
            [GoogleMerchantMpnFallbackStrategy.None] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.MpnFallbackStrategy.None",
            [GoogleMerchantMpnFallbackStrategy.MpnAttribute] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.MpnFallbackStrategy.MpnAttribute"
        });

        model.AvailableProductConditions = await BuildSelectListAsync(model.DefaultCondition, new Dictionary<GoogleMerchantProductCondition, string>
        {
            [GoogleMerchantProductCondition.New] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.New",
            [GoogleMerchantProductCondition.Refurbished] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.Refurbished",
            [GoogleMerchantProductCondition.Used] = $"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCondition.Used"
        });
    }

    private async Task<IList<SelectListItem>> BuildSelectListAsync<TEnum>(TEnum selectedValue, IDictionary<TEnum, string> resourceKeys) where TEnum : struct, Enum
    {
        var values = new List<SelectListItem>();

        foreach (var pair in resourceKeys)
        {
            values.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync(pair.Value),
                Value = pair.Key.ToString(),
                Selected = EqualityComparer<TEnum>.Default.Equals(selectedValue, pair.Key)
            });
        }

        return values;
    }

    private string BuildAbsoluteRouteUrl(string routeName, object routeValues)
    {
        var relativeUrl = _nopUrlHelper.RouteUrl(routeName, routeValues);
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return string.Empty;

        return new Uri(new Uri(_webHelper.GetStoreLocation()), relativeUrl.TrimStart('/')).ToString();
    }

    private static IList<int> ParseStoreIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<int>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(storeId => int.TryParse(storeId, out var parsedStoreId) ? parsedStoreId : (int?)null)
            .Where(storeId => storeId.HasValue && storeId.Value > 0)
            .Select(storeId => storeId.Value)
            .Distinct()
            .ToList();
    }
}
