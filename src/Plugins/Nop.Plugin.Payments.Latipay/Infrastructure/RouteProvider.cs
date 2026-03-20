using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace Nop.Plugin.Payments.Latipay.Infrastructure;

/// <summary>
/// Registers plugin routes.
/// </summary>
public class RouteProvider : BaseRouteProvider, IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var lang = GetLanguageRoutePattern();

        endpointRouteBuilder.MapControllerRoute(
            name: LatipayDefaults.Route.Configuration,
            pattern: "Admin/Latipay/Configure",
            defaults: new { controller = "Latipay", action = "Configure", area = AreaNames.ADMIN });

        endpointRouteBuilder.MapControllerRoute(
            name: LatipayDefaults.Route.ManualReconcile,
            pattern: "Admin/Latipay/ManualReconcile",
            defaults: new { controller = "Latipay", action = "ManualReconcile", area = AreaNames.ADMIN });

        endpointRouteBuilder.MapControllerRoute(
            name: LatipayDefaults.Route.Retry,
            pattern: $"{lang}/latipay/retry/{{orderId:int}}",
            defaults: new { controller = "LatipayPublic", action = "Retry" });

        endpointRouteBuilder.MapControllerRoute(
            name: LatipayDefaults.Route.Return,
            pattern: $"{lang}/latipay/return",
            defaults: new { controller = "LatipayPublic", action = "Return" });

        endpointRouteBuilder.MapControllerRoute(
            name: LatipayDefaults.Route.Callback,
            pattern: "Plugins/Latipay/Callback",
            defaults: new { controller = "LatipayPublic", action = "Callback" });
    }

    public int Priority => 0;
}
