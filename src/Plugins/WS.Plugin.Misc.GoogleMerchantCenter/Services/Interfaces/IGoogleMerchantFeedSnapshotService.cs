using WS.Plugin.Misc.GoogleMerchantCenter.Domain;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

public interface IGoogleMerchantFeedSnapshotService
{
    Task<GoogleMerchantGenerationResult> GetFeedAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default);

    Task<GoogleMerchantGenerationResult> RegenerateAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default);

    Task InvalidateSnapshotsAsync(CancellationToken cancellationToken = default);
}
