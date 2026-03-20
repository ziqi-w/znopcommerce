namespace Nop.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents whether a nopCommerce order is currently safe to retry through Latipay.
/// </summary>
public class LatipayRetryEligibilityResult
{
    public bool CanRetry { get; set; }

    public string Message { get; set; }
}
