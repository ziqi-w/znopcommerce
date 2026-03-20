using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Latipay.Models.Public;

/// <summary>
/// Represents the public return status model.
/// </summary>
public record ReturnStatusModel : BaseNopModel
{
    public int? OrderId { get; set; }

    public string MerchantReference { get; set; }

    public string Status { get; set; }

    public string Message { get; set; }
}
