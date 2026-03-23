using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace WS.Plugin.Payments.Latipay.Models.Public;

/// <summary>
/// Represents the checkout payment info model.
/// </summary>
public record PaymentInfoModel : BaseNopModel
{
    public PaymentInfoModel()
    {
        AvailableSubPaymentMethods = [];
    }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.SubPaymentMethod")]
    public string SelectedSubPaymentMethod { get; set; }

    public IList<LatipaySubPaymentMethodModel> AvailableSubPaymentMethods { get; set; }

    public bool HasAvailableSubPaymentMethods => AvailableSubPaymentMethods.Any();

    public bool HasSingleAvailableSubPaymentMethod => AvailableSubPaymentMethods.Count == 1;
}
