using Nop.Web.Framework.Models;

namespace WS.Plugin.Payments.Latipay.Models.Public;

/// <summary>
/// Represents a branded Latipay payment option rendered in the public checkout UI.
/// </summary>
public record LatipaySubPaymentMethodModel : BaseNopModel
{
    public string Key { get; init; }

    public string DisplayName { get; init; }

    public string LogoUrl { get; init; }
}
