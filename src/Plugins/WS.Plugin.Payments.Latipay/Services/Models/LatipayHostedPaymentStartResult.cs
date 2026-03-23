namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents the outcome of starting a hosted Latipay payment attempt.
/// </summary>
public class LatipayHostedPaymentStartResult
{
    public bool Started { get; set; }

    public int OrderId { get; set; }

    public int? PaymentAttemptId { get; set; }

    public string MerchantReference { get; set; }

    public string HostedPaymentUrl { get; set; }

    public string Message { get; set; }
}
