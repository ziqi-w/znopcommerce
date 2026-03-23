using WS.Plugin.Misc.GoogleMerchantCenter.Domain;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

public interface IGoogleMerchantProductMappingService
{
    Task<GoogleMerchantMappingResult> MapAsync(IReadOnlyCollection<GoogleMerchantEligibleProduct> eligibleProducts, GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default);
}
