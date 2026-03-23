namespace WS.Plugin.Payments.Latipay.Services.Models;

/// <summary>
/// Represents whether a refund can be safely submitted to Latipay.
/// </summary>
public class LatipayRefundEligibilityResult
{
    public bool CanRefund { get; set; }

    public int? PaymentAttemptId { get; set; }

    public string LatipayOrderId { get; set; }

    public decimal RemainingRefundableAmount { get; set; }

    public string Message { get; set; }
}
