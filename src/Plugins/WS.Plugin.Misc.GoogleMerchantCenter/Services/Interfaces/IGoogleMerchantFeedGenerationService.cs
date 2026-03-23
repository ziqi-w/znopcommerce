using WS.Plugin.Misc.GoogleMerchantCenter.Domain;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

public interface IGoogleMerchantFeedGenerationService
{
    Task<GoogleMerchantGenerationResult> GenerateAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default);
}
