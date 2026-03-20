using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Latipay;

/// <summary>
/// Represents Latipay plugin settings.
/// </summary>
public class LatipaySettings : ISettings
{
    public bool Enabled { get; set; }

    public bool UseSandbox { get; set; }

    public string UserId { get; set; }

    public string WalletId { get; set; }

    public string ApiKey { get; set; }

    public string ApiBaseUrl { get; set; }

    public string CardApiBaseUrl { get; set; }

    public string CardMerchantId { get; set; }

    public string CardSiteId { get; set; }

    public string CardPrivateKey { get; set; }

    public int RequestTimeoutSeconds { get; set; }

    public bool DebugLogging { get; set; }

    public bool EnableAlipay { get; set; }

    public string AlipayDisplayName { get; set; }

    public bool EnableWechatPay { get; set; }

    public string WechatPayDisplayName { get; set; }

    public bool EnableNzBanks { get; set; }

    public string NzBanksDisplayName { get; set; }

    public bool EnablePayId { get; set; }

    public string PayIdDisplayName { get; set; }

    public bool EnableUpiUpop { get; set; }

    public string UpiUpopDisplayName { get; set; }

    public bool EnableCardVm { get; set; }

    public string CardVmDisplayName { get; set; }

    public bool EnableRefunds { get; set; }

    public bool EnablePartialRefunds { get; set; }

    public bool EnableReconciliationTask { get; set; }

    public int ReconciliationPeriodMinutes { get; set; }

    public int RetryGuardMinutes { get; set; }
}
