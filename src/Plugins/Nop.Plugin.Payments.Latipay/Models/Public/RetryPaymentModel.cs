using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Latipay.Models.Public;

/// <summary>
/// Represents the retry payment model.
/// </summary>
public record RetryPaymentModel : BaseNopModel
{
    public RetryPaymentModel()
    {
        AvailableSubPaymentMethods = [];
    }

    public int OrderId { get; set; }

    public string OrderNumber { get; set; }

    public bool CanRetry { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.SubPaymentMethod")]
    public string SelectedSubPaymentMethod { get; set; }

    public IList<LatipaySubPaymentMethodModel> AvailableSubPaymentMethods { get; set; }

    public string Message { get; set; }
}
