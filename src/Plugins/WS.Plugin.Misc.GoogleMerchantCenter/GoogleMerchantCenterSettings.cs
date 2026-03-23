using Nop.Core.Configuration;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain.Enums;

namespace WS.Plugin.Misc.GoogleMerchantCenter;

public class GoogleMerchantCenterSettings : ISettings
{
    public bool Enabled { get; set; }

    public string FeedToken { get; set; }

    public GoogleMerchantFeedFormat FeedFormat { get; set; }

    public string DefaultCurrencyCode { get; set; }

    public string DefaultCountryCode { get; set; }

    public bool IncludeUnpublishedProducts { get; set; }

    public bool IncludeProductsWithoutImages { get; set; }

    public bool IncludeOutOfStockProducts { get; set; }

    public string LimitedToStoreIdsCsv { get; set; }

    public bool IncludeShipping { get; set; }

    public string DefaultShippingCountryCode { get; set; }

    public string DefaultShippingService { get; set; }

    public decimal DefaultShippingPrice { get; set; }

    public GoogleMerchantBrandFallbackStrategy BrandFallbackStrategy { get; set; }

    public GoogleMerchantGtinFallbackStrategy GtinFallbackStrategy { get; set; }

    public GoogleMerchantMpnFallbackStrategy MpnFallbackStrategy { get; set; }

    public GoogleMerchantProductCondition DefaultCondition { get; set; }

    public bool ExportAdditionalImageLinks { get; set; }

    public int FeedRegenerationIntervalMinutes { get; set; }

    public DateTime? LastGenerationUtc { get; set; }

    public string LastGenerationStatus { get; set; }

    public int LastGeneratedItemCount { get; set; }

    public int LastSkippedItemCount { get; set; }

    public int LastWarningCount { get; set; }

    public int LastErrorCount { get; set; }

    public string LastGenerationSummary { get; set; }

    public string LastGenerationMessagesJson { get; set; }
}
