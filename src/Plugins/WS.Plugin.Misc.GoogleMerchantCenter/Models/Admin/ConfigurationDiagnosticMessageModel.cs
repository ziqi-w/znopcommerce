using Nop.Web.Framework.Models;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Models.Admin;

public record ConfigurationDiagnosticMessageModel : BaseNopModel
{
    public string Severity { get; init; }

    public string Code { get; init; }

    public int? ProductId { get; init; }

    public string Message { get; init; }
}
