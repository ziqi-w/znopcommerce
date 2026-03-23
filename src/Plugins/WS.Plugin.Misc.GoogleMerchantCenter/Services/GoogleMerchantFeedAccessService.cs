using System.Security.Cryptography;
using System.Text;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantFeedAccessService : IGoogleMerchantFeedAccessService
{
    private readonly GoogleMerchantCenterSettings _settings;

    public GoogleMerchantFeedAccessService(GoogleMerchantCenterSettings settings)
    {
        _settings = settings;
    }

    public bool IsRequestAuthorized(string token)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.FeedToken) || string.IsNullOrWhiteSpace(token))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(_settings.FeedToken.Trim());
        var actualBytes = Encoding.UTF8.GetBytes(token.Trim());

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
