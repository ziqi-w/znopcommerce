using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace WS.Plugin.Payments.Latipay.Models.Admin;

/// <summary>
/// Represents the admin configuration model.
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.UseSandbox")]
    public bool UseSandbox { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.UserId")]
    public string UserId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.WalletId")]
    public string WalletId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.ApiKey")]
    public string ApiKey { get; set; }

    public bool ApiKeyConfigured { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.ApiBaseUrl")]
    public string ApiBaseUrl { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.CardApiBaseUrl")]
    public string CardApiBaseUrl { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.CardMerchantId")]
    public string CardMerchantId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.CardSiteId")]
    public string CardSiteId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.CardPrivateKey")]
    public string CardPrivateKey { get; set; }

    public bool CardPrivateKeyConfigured { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.RequestTimeoutSeconds")]
    public int RequestTimeoutSeconds { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.SupportedCurrencyCode")]
    public string SupportedCurrencyCode { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.DebugLogging")]
    public bool DebugLogging { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableAlipay")]
    public bool EnableAlipay { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.AlipayDisplayName")]
    public string AlipayDisplayName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableWechatPay")]
    public bool EnableWechatPay { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.WechatPayDisplayName")]
    public string WechatPayDisplayName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableNzBanks")]
    public bool EnableNzBanks { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.NzBanksDisplayName")]
    public string NzBanksDisplayName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnablePayId")]
    public bool EnablePayId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.PayIdDisplayName")]
    public string PayIdDisplayName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableUpiUpop")]
    public bool EnableUpiUpop { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.UpiUpopDisplayName")]
    public string UpiUpopDisplayName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableCardVm")]
    public bool EnableCardVm { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.CardVmDisplayName")]
    public string CardVmDisplayName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableRefunds")]
    public bool EnableRefunds { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnablePartialRefunds")]
    public bool EnablePartialRefunds { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.EnableReconciliationTask")]
    public bool EnableReconciliationTask { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.ReconciliationPeriodMinutes")]
    public int ReconciliationPeriodMinutes { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.RetryGuardMinutes")]
    public int RetryGuardMinutes { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.ManualReconcileOrderId")]
    public int? ManualReconcileOrderId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.Latipay.Fields.ManualReconcileMerchantReference")]
    public string ManualReconcileMerchantReference { get; set; }
}
