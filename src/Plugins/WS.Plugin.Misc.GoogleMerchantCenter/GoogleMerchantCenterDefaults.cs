namespace WS.Plugin.Misc.GoogleMerchantCenter;

public static class GoogleMerchantCenterDefaults
{
    public const string SystemName = "Misc.GoogleMerchantCenter";
    public const string LegacySystemName = "WS.GoogleMerchantCenter";
    public const string ConfigurationRouteName = "Plugin.WS.Plugin.Misc.GoogleMerchantCenter.Configure";
    public const string FeedRouteName = "Plugin.WS.Plugin.Misc.GoogleMerchantCenter.Feed";
    public const string PluginFeedRouteName = "Plugin.WS.Plugin.Misc.GoogleMerchantCenter.PluginFeed";
    public const string LegacyPluginFeedRouteName = "Plugin.WS.GoogleMerchantCenter.PluginFeed";
    public const string ScheduleTaskName = "WS Google Merchant Center feed regeneration";
    public const string ScheduleTaskType = "WS.Plugin.Misc.GoogleMerchantCenter.Services.GoogleMerchantFeedRegenerationTask, WS.Plugin.Misc.GoogleMerchantCenter";
    public const string LegacyScheduleTaskType = "WS.GoogleMerchantCenter.Services.GoogleMerchantFeedRegenerationTask, WS.GoogleMerchantCenter";
    public const string ConfigureViewPath = "~/Plugins/WS.Plugin.Misc.GoogleMerchantCenter/Views/Configure.cshtml";
    public const string PluginFeedPath = "Plugins/Misc.GoogleMerchantCenter/Feed";
    public const string LegacyPluginFeedPath = "Plugins/WS.GoogleMerchantCenter/Feed";
    // Keep the existing resource prefix so already-installed environments continue to resolve admin labels after the SystemName rename.
    public const string LocaleResourcePrefix = "Plugins.WS.GoogleMerchantCenter";
    public const string TokenQueryParameterName = "token";
    public const string GoogleNamespace = "http://base.google.com/ns/1.0";
    public const string XmlContentType = "application/xml";
    public const string DefaultCurrencyCode = "NZD";
    public const string DefaultCountryCode = "NZ";
    public const int DefaultFeedRegenerationIntervalMinutes = 60;
    public const int MinFeedRegenerationIntervalMinutes = 5;
    public const int MaxFeedRegenerationIntervalMinutes = 1440;
    public const int MaxFeedTokenLength = 128;
    public const int MaxCurrencyCodeLength = 3;
    public const int MaxCountryCodeLength = 2;
    public const int MaxShippingServiceLength = 100;
    public const int MaxSummaryLength = 4000;
    public const int MaxTitleLength = 150;
    public const int MaxDescriptionLength = 5000;
    public const int MaxPersistedDiagnosticMessages = 100;
    public const int MaxDiagnosticMessageLength = 500;
    public static string SnapshotDirectoryPath => "~/App_Data/WS.Plugin.Misc.GoogleMerchantCenter";
}
