using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Infrastructure;

public class RouteProvider : BaseRouteProvider, IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: GoogleMerchantCenterDefaults.ConfigurationRouteName,
            pattern: "Admin/GoogleMerchantCenter/Configure",
            defaults: new { controller = "GoogleMerchantCenter", action = "Configure", area = AreaNames.ADMIN });

        endpointRouteBuilder.MapControllerRoute(
            name: GoogleMerchantCenterDefaults.FeedRouteName,
            pattern: "google-merchant-feed",
            defaults: new { controller = "GoogleMerchantFeed", action = "Feed" });

        endpointRouteBuilder.MapControllerRoute(
            name: GoogleMerchantCenterDefaults.PluginFeedRouteName,
            pattern: GoogleMerchantCenterDefaults.PluginFeedPath,
            defaults: new { controller = "GoogleMerchantFeed", action = "Feed" });

        endpointRouteBuilder.MapControllerRoute(
            name: GoogleMerchantCenterDefaults.LegacyPluginFeedRouteName,
            pattern: GoogleMerchantCenterDefaults.LegacyPluginFeedPath,
            defaults: new { controller = "GoogleMerchantFeed", action = "Feed" });
    }

    public int Priority => 0;
}
