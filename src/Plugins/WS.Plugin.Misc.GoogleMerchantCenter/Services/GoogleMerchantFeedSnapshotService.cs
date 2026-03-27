using System.Collections.Concurrent;
using System.Text;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Services.Logging;
using Nop.Services.Stores;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantFeedSnapshotService : IGoogleMerchantFeedSnapshotService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SnapshotLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly IGoogleMerchantDiagnosticsService _diagnosticsService;
    private readonly IGoogleMerchantFeedGenerationService _feedGenerationService;
    private readonly INopFileProvider _fileProvider;
    private readonly ILogger _logger;
    private readonly GoogleMerchantCenterSettings _settings;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;

    public GoogleMerchantFeedSnapshotService(IGoogleMerchantDiagnosticsService diagnosticsService,
        IGoogleMerchantFeedGenerationService feedGenerationService,
        INopFileProvider fileProvider,
        ILogger logger,
        GoogleMerchantCenterSettings settings,
        IStoreContext storeContext,
        IStoreService storeService)
    {
        _diagnosticsService = diagnosticsService;
        _feedGenerationService = feedGenerationService;
        _fileProvider = fileProvider;
        _logger = logger;
        _settings = settings;
        _storeContext = storeContext;
        _storeService = storeService;
    }

    public async Task<GoogleMerchantGenerationResult> GetFeedAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = await NormalizeRequestAsync(request);
        var snapshotPath = GetSnapshotPath(normalizedRequest);

        if (!normalizedRequest.ForceRegeneration && await TryLoadFreshSnapshotAsync(snapshotPath, cancellationToken) is { } freshSnapshot)
            return freshSnapshot;

        return await ExecuteWithLockAsync(snapshotPath, async () =>
        {
            if (!normalizedRequest.ForceRegeneration && await TryLoadFreshSnapshotAsync(snapshotPath, cancellationToken) is { } lockedFreshSnapshot)
                return lockedFreshSnapshot;

            var generatedResult = await _feedGenerationService.GenerateAsync(normalizedRequest, cancellationToken);
            if (generatedResult.Succeeded && !string.IsNullOrWhiteSpace(generatedResult.FeedContent))
            {
                await SaveSnapshotAsync(snapshotPath, generatedResult.FeedContent);
                return generatedResult;
            }

            if (await TryLoadSnapshotAsync(snapshotPath, cancellationToken) is { } staleSnapshot)
            {
                await _logger.WarningAsync($"{GoogleMerchantCenterDefaults.SystemName} feed regeneration failed, so stale snapshot '{snapshotPath}' was served instead.");
                return staleSnapshot;
            }

            return generatedResult;
        });
    }

    public async Task<GoogleMerchantGenerationResult> RegenerateAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = await NormalizeRequestAsync(new GoogleMerchantFeedRequest
        {
            ForceRegeneration = true,
            StoreId = request.StoreId,
            LanguageId = request.LanguageId,
            CurrencyCode = request.CurrencyCode,
            CountryCode = request.CountryCode
        });
        var snapshotPath = GetSnapshotPath(normalizedRequest);

        return await ExecuteWithLockAsync(snapshotPath, async () =>
        {
            var generatedResult = await _feedGenerationService.GenerateAsync(normalizedRequest, cancellationToken);
            if (generatedResult.Succeeded && !string.IsNullOrWhiteSpace(generatedResult.FeedContent))
                await SaveSnapshotAsync(snapshotPath, generatedResult.FeedContent);

            return generatedResult;
        });
    }

    public Task InvalidateSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var snapshotDirectoryPath = _fileProvider.MapPath(GoogleMerchantCenterDefaults.SnapshotDirectoryPath);
        if (_fileProvider.DirectoryExists(snapshotDirectoryPath))
            _fileProvider.DeleteDirectory(snapshotDirectoryPath);

        return Task.CompletedTask;
    }

    private async Task<GoogleMerchantFeedRequest> NormalizeRequestAsync(GoogleMerchantFeedRequest request)
    {
        var storeId = request.StoreId.GetValueOrDefault() > 0
            ? request.StoreId.Value
            : (await _storeContext.GetCurrentStoreAsync())?.Id ?? 0;
        var store = storeId > 0
            ? await _storeService.GetStoreByIdAsync(storeId)
            : await _storeContext.GetCurrentStoreAsync();
        var languageId = request.LanguageId.GetValueOrDefault() > 0
            ? request.LanguageId.Value
            : store?.DefaultLanguageId ?? 0;

        return new GoogleMerchantFeedRequest
        {
            ForceRegeneration = request.ForceRegeneration,
            StoreId = storeId > 0 ? storeId : null,
            LanguageId = languageId > 0 ? languageId : null,
            CurrencyCode = GoogleMerchantFeedRequestNormalizer.NormalizeCurrencyCode(request.CurrencyCode)
                ?? GoogleMerchantFeedRequestNormalizer.NormalizeCurrencyCode(_settings.DefaultCurrencyCode)
                ?? GoogleMerchantCenterDefaults.DefaultCurrencyCode,
            CountryCode = GoogleMerchantFeedRequestNormalizer.NormalizeCountryCode(request.CountryCode)
                ?? GoogleMerchantFeedRequestNormalizer.NormalizeCountryCode(_settings.DefaultCountryCode)
                ?? GoogleMerchantCenterDefaults.DefaultCountryCode
        };
    }

    private async Task<GoogleMerchantGenerationResult> TryLoadFreshSnapshotAsync(string snapshotPath, CancellationToken cancellationToken)
    {
        if (!_fileProvider.FileExists(snapshotPath))
            return null;

        var intervalMinutes = Math.Clamp(_settings.FeedRegenerationIntervalMinutes,
            GoogleMerchantCenterDefaults.MinFeedRegenerationIntervalMinutes,
            GoogleMerchantCenterDefaults.MaxFeedRegenerationIntervalMinutes);
        var lastWriteUtc = _fileProvider.GetLastWriteTimeUtc(snapshotPath);

        if (DateTime.UtcNow - lastWriteUtc > TimeSpan.FromMinutes(intervalMinutes))
            return null;

        return await LoadSnapshotAsync(snapshotPath, cancellationToken);
    }

    private async Task<GoogleMerchantGenerationResult> TryLoadSnapshotAsync(string snapshotPath, CancellationToken cancellationToken)
    {
        if (!_fileProvider.FileExists(snapshotPath))
            return null;

        return await LoadSnapshotAsync(snapshotPath, cancellationToken);
    }

    private async Task<GoogleMerchantGenerationResult> LoadSnapshotAsync(string snapshotPath, CancellationToken cancellationToken)
    {
        var feedContent = await _fileProvider.ReadAllTextAsync(snapshotPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(feedContent))
            return null;

        return new GoogleMerchantGenerationResult
        {
            Succeeded = true,
            GeneratedOnUtc = _fileProvider.GetLastWriteTimeUtc(snapshotPath),
            FeedContent = feedContent,
            Diagnostics = await _diagnosticsService.GetLastSummaryAsync(cancellationToken)
        };
    }

    private async Task SaveSnapshotAsync(string snapshotPath, string feedContent)
    {
        var directoryPath = _fileProvider.GetDirectoryName(snapshotPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
            _fileProvider.CreateDirectory(directoryPath);

        await _fileProvider.WriteAllTextAsync(snapshotPath, feedContent, Encoding.UTF8);
    }

    private string GetSnapshotPath(GoogleMerchantFeedRequest request)
    {
        var snapshotDirectoryPath = _fileProvider.MapPath(GoogleMerchantCenterDefaults.SnapshotDirectoryPath);
        var currencyCode = GoogleMerchantFeedRequestNormalizer.NormalizeSnapshotSegment(
            request.CurrencyCode,
            GoogleMerchantCenterDefaults.DefaultCurrencyCode);
        var countryCode = GoogleMerchantFeedRequestNormalizer.NormalizeSnapshotSegment(
            request.CountryCode,
            GoogleMerchantCenterDefaults.DefaultCountryCode);
        var fileName = $"feed-s{request.StoreId.GetValueOrDefault()}-l{request.LanguageId.GetValueOrDefault()}-c{currencyCode}-ctry{countryCode}.xml";
        return _fileProvider.Combine(snapshotDirectoryPath, fileName);
    }

    private static async Task<GoogleMerchantGenerationResult> ExecuteWithLockAsync(string snapshotPath, Func<Task<GoogleMerchantGenerationResult>> action)
    {
        var snapshotLock = SnapshotLocks.GetOrAdd(snapshotPath, _ => new SemaphoreSlim(1, 1));
        await snapshotLock.WaitAsync();

        try
        {
            return await action();
        }
        finally
        {
            snapshotLock.Release();
        }
    }

}
