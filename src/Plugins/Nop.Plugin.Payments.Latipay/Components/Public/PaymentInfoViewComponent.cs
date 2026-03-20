using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.Latipay.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Latipay.Components.Public;

/// <summary>
/// Renders the checkout payment info UI.
/// </summary>
public class PaymentInfoViewComponent : NopViewComponent
{
    private readonly LatipayModelFactory _latipayModelFactory;

    public PaymentInfoViewComponent(LatipayModelFactory latipayModelFactory)
    {
        _latipayModelFactory = latipayModelFactory;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _latipayModelFactory.PreparePaymentInfoModelAsync();
        return View("~/Plugins/Payments.Latipay/Views/Public/PaymentInfo.cshtml", model);
    }
}
