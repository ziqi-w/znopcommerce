using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Web.Controllers;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Controllers;

public class GoogleMerchantFeedController : BasePublicController
{
    private readonly IGoogleMerchantFeedAccessService _feedAccessService;
    private readonly IGoogleMerchantFeedSnapshotService _feedSnapshotService;
    private readonly GoogleMerchantCenterSettings _settings;

    public GoogleMerchantFeedController(IGoogleMerchantFeedAccessService feedAccessService,
        IGoogleMerchantFeedSnapshotService feedSnapshotService,
        GoogleMerchantCenterSettings settings)
    {
        _feedAccessService = feedAccessService;
        _feedSnapshotService = feedSnapshotService;
        _settings = settings;
    }

    [HttpGet]
    public async Task<IActionResult> Feed(string token, int? storeId = null, int? languageId = null, string currency = null, CancellationToken cancellationToken = default)
    {
        if (!_feedAccessService.IsRequestAuthorized(token))
            return NotFound();

        var result = await _feedSnapshotService.GetFeedAsync(new GoogleMerchantFeedRequest
        {
            StoreId = storeId,
            LanguageId = languageId,
            CurrencyCode = string.IsNullOrWhiteSpace(currency) ? _settings.DefaultCurrencyCode : currency.Trim().ToUpperInvariant(),
            CountryCode = _settings.DefaultCountryCode,
            ForceRegeneration = false
        }, cancellationToken);

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.FeedContent))
            return StatusCode(503);

        return Content(result.FeedContent, result.ContentType, Encoding.UTF8);
    }
}
