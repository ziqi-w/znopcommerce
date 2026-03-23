using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantFeedRegenerationTask : IScheduleTask
{
    private readonly IGoogleMerchantFeedSnapshotService _feedSnapshotService;
    private readonly ILogger _logger;
    private readonly ISettingService _settingService;
    private readonly IStoreService _storeService;

    public GoogleMerchantFeedRegenerationTask(IGoogleMerchantFeedSnapshotService feedSnapshotService,
        ILogger logger,
        ISettingService settingService,
        IStoreService storeService)
    {
        _feedSnapshotService = feedSnapshotService;
        _logger = logger;
        _settingService = settingService;
        _storeService = storeService;
    }

    public async Task ExecuteAsync()
    {
        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();
        if (!settings.Enabled)
            return;

        var configuredStoreIds = ParseStoreIds(settings.LimitedToStoreIdsCsv);
        var stores = await _storeService.GetAllStoresAsync();
        var targetStores = configuredStoreIds.Count == 0
            ? stores
            : stores.Where(store => configuredStoreIds.Contains(store.Id)).ToList();

        if (targetStores.Count == 0)
            targetStores = stores;

        foreach (var store in targetStores)
        {
            try
            {
                await _feedSnapshotService.RegenerateAsync(new GoogleMerchantFeedRequest
                {
                    ForceRegeneration = true,
                    StoreId = store.Id,
                    CurrencyCode = settings.DefaultCurrencyCode,
                    CountryCode = settings.DefaultCountryCode
                });
            }
            catch (Exception exception)
            {
                await _logger.ErrorAsync($"{GoogleMerchantCenterDefaults.SystemName} scheduled feed regeneration failed for store {store.Id}.", exception);
            }
        }
    }

    private static HashSet<int> ParseStoreIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<int>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(storeId => int.TryParse(storeId, out var parsedStoreId) ? (int?)parsedStoreId : null)
            .Where(storeId => storeId.HasValue && storeId.Value > 0)
            .Select(storeId => storeId!.Value)
            .ToHashSet();
    }
}
