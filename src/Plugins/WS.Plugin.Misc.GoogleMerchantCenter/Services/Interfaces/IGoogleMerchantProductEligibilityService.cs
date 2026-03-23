using WS.Plugin.Misc.GoogleMerchantCenter.Domain;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

public interface IGoogleMerchantProductEligibilityService
{
    Task<GoogleMerchantEligibilityResult> GetEligibleProductsAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default);
}
