using WS.Plugin.Misc.GoogleMerchantCenter.Domain;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

public interface IGoogleMerchantDiagnosticsService
{
    Task<GoogleMerchantDiagnosticsSummary> GetLastSummaryAsync(CancellationToken cancellationToken = default);

    Task SaveGenerationResultAsync(GoogleMerchantGenerationResult result, CancellationToken cancellationToken = default);
}
