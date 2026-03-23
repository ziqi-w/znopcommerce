using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.ModelBinding;
using WS.Plugin.Misc.GoogleMerchantCenter;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain.Enums;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Models.Admin;

public record ConfigurationModel : BaseNopModel
{
    public ConfigurationModel()
    {
        SelectedStoreIds = new List<int>();
        LastGenerationMessages = new List<ConfigurationDiagnosticMessageModel>();
        AvailableStores = new List<SelectListItem>();
        AvailableFeedFormats = new List<SelectListItem>();
        AvailableBrandFallbackStrategies = new List<SelectListItem>();
        AvailableGtinFallbackStrategies = new List<SelectListItem>();
        AvailableMpnFallbackStrategies = new List<SelectListItem>();
        AvailableProductConditions = new List<SelectListItem>();
    }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.FeedToken")]
    [NoTrim]
    public string FeedToken { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.FeedFormat")]
    public GoogleMerchantFeedFormat FeedFormat { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.DefaultCurrencyCode")]
    public string DefaultCurrencyCode { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.DefaultCountryCode")]
    public string DefaultCountryCode { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.IncludeUnpublishedProducts")]
    public bool IncludeUnpublishedProducts { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.IncludeProductsWithoutImages")]
    public bool IncludeProductsWithoutImages { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.IncludeOutOfStockProducts")]
    public bool IncludeOutOfStockProducts { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.SelectedStoreIds")]
    public IList<int> SelectedStoreIds { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.IncludeShipping")]
    public bool IncludeShipping { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.DefaultShippingCountryCode")]
    public string DefaultShippingCountryCode { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.DefaultShippingService")]
    public string DefaultShippingService { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.DefaultShippingPrice")]
    public decimal DefaultShippingPrice { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.BrandFallbackStrategy")]
    public GoogleMerchantBrandFallbackStrategy BrandFallbackStrategy { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.GtinFallbackStrategy")]
    public GoogleMerchantGtinFallbackStrategy GtinFallbackStrategy { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.MpnFallbackStrategy")]
    public GoogleMerchantMpnFallbackStrategy MpnFallbackStrategy { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.DefaultCondition")]
    public GoogleMerchantProductCondition DefaultCondition { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.ExportAdditionalImageLinks")]
    public bool ExportAdditionalImageLinks { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.FeedRegenerationIntervalMinutes")]
    public int FeedRegenerationIntervalMinutes { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.FeedUrl")]
    public string FeedUrl { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.PluginFeedUrl")]
    public string PluginFeedUrl { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastGenerationUtc")]
    public DateTime? LastGenerationUtc { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastGenerationStatus")]
    public string LastGenerationStatus { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastGeneratedItemCount")]
    public int LastGeneratedItemCount { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastSkippedItemCount")]
    public int LastSkippedItemCount { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastWarningCount")]
    public int LastWarningCount { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastErrorCount")]
    public int LastErrorCount { get; set; }

    [NopResourceDisplayName(GoogleMerchantCenterDefaults.LocaleResourcePrefix + ".Fields.LastGenerationSummary")]
    public string LastGenerationSummary { get; set; }

    public IList<ConfigurationDiagnosticMessageModel> LastGenerationMessages { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; }

    public IList<SelectListItem> AvailableFeedFormats { get; set; }

    public IList<SelectListItem> AvailableBrandFallbackStrategies { get; set; }

    public IList<SelectListItem> AvailableGtinFallbackStrategies { get; set; }

    public IList<SelectListItem> AvailableMpnFallbackStrategies { get; set; }

    public IList<SelectListItem> AvailableProductConditions { get; set; }
}
