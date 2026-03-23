namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents the outcome of applying a verified payment state update.
/// </summary>
public class LatipayStateTransitionResult
{
    public bool Changed { get; set; }

    public bool MarkedPaid { get; set; }

    public bool ReviewRequired { get; set; }

    public bool KeepPending { get; set; }

    public string AppliedStatus { get; set; }

    public string Message { get; set; }
}
