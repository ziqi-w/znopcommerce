using Nop.Core.Domain.ScheduleTasks;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Latipay;

/// <summary>
/// Represents the shared plugin scaffold for Latipay.
/// </summary>
public abstract class LatipayPlugin : BasePlugin
{
    private readonly ILocalizationService _localizationService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly ISettingService _settingService;

    protected LatipayPlugin(ILocalizationService localizationService,
        INopUrlHelper nopUrlHelper,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService)
    {
        _localizationService = localizationService;
        _nopUrlHelper = nopUrlHelper;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
    }

    public override string GetConfigurationPageUrl()
    {
        return _nopUrlHelper.RouteUrl(LatipayDefaults.Route.Configuration);
    }

    protected string GetRouteUrl(string routeName, object values = null, string protocol = null, string host = null, string fragment = null)
    {
        return _nopUrlHelper.RouteUrl(routeName, values, protocol, host, fragment);
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new LatipaySettings
        {
            Enabled = false,
            UseSandbox = false,
            ApiBaseUrl = LatipayDefaults.ApiBaseUrl,
            CardApiBaseUrl = LatipayDefaults.CardApiBaseUrl,
            RequestTimeoutSeconds = LatipayDefaults.DefaultRequestTimeoutSeconds,
            DebugLogging = false,
            EnableAlipay = true,
            AlipayDisplayName = LatipayDefaults.DefaultAlipayDisplayName,
            EnableWechatPay = true,
            WechatPayDisplayName = LatipayDefaults.DefaultWechatPayDisplayName,
            EnableNzBanks = true,
            NzBanksDisplayName = LatipayDefaults.DefaultNzBanksDisplayName,
            EnablePayId = false,
            PayIdDisplayName = LatipayDefaults.DefaultPayIdDisplayName,
            EnableUpiUpop = false,
            UpiUpopDisplayName = LatipayDefaults.DefaultUpiUpopDisplayName,
            EnableCardVm = false,
            CardVmDisplayName = LatipayDefaults.DefaultCardVmDisplayName,
            EnableRefunds = true,
            EnablePartialRefunds = true,
            EnableReconciliationTask = false,
            ReconciliationPeriodMinutes = LatipayDefaults.DefaultReconciliationPeriodMinutes,
            RetryGuardMinutes = LatipayDefaults.DefaultRetryGuardMinutes
        });

        if (await _scheduleTaskService.GetTaskByTypeAsync(LatipayDefaults.ReconciliationTaskType) is null)
        {
            await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
            {
                Name = LatipayDefaults.ReconciliationTaskName,
                Type = LatipayDefaults.ReconciliationTaskType,
                Enabled = false,
                Seconds = LatipayDefaults.DefaultReconciliationPeriodMinutes * 60,
                StopOnError = false,
                LastEnabledUtc = DateTime.UtcNow
            });
        }

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.Latipay.Configuration"] = "Configuration",
            ["Plugins.Payments.Latipay.Configuration.General"] = "General",
            ["Plugins.Payments.Latipay.Configuration.Credentials"] = "Credentials",
            ["Plugins.Payments.Latipay.Configuration.LegacyCredentials"] = "Legacy hosted-online credentials",
            ["Plugins.Payments.Latipay.Configuration.LegacyCredentials.Help"] = "These credentials apply to the original Latipay hosted-online methods such as Alipay, WeChat Pay, NZ Banks, PayID, and UnionPay UPOP.",
            ["Plugins.Payments.Latipay.Configuration.CardCredentials"] = "Hosted card credentials",
            ["Plugins.Payments.Latipay.Configuration.CardCredentials.Help"] = "These credentials apply to the hosted card payment pages used for the combined Visa/Mastercard option. Card payments use a separate Latipay API contract and separate signing key.",
            ["Plugins.Payments.Latipay.Configuration.PaymentMethods"] = "Payment methods",
            ["Plugins.Payments.Latipay.Configuration.Advanced"] = "Advanced",
            ["Plugins.Payments.Latipay.Configuration.Operations"] = "Operations",
            ["Plugins.Payments.Latipay.Configuration.Instructions"] = "Configure the Latipay hosted redirect integration, credentials, and checkout options. The payment method is intentionally limited to NZD transactions.",
            ["Plugins.Payments.Latipay.Configuration.CurrencyNotice"] = "NZD only. This plugin does not offer any currency other than New Zealand dollar.",
            ["Plugins.Payments.Latipay.Configuration.StoreScopeNotice"] = "This plugin currently saves configuration globally. Store-specific overrides are not implemented yet.",
            ["Plugins.Payments.Latipay.Configuration.ApiKeyConfigured"] = "A Latipay API key is already stored for this plugin.",
            ["Plugins.Payments.Latipay.Configuration.ApiKeyNotConfigured"] = "No Latipay API key is stored yet.",
            ["Plugins.Payments.Latipay.Configuration.ApiKeyReplaceHint"] = "Leave the API key field blank to keep the existing secret. Enter a new value only when you want to replace it.",
            ["Plugins.Payments.Latipay.Configuration.CardPrivateKeyConfigured"] = "A hosted card private key is already stored for this plugin.",
            ["Plugins.Payments.Latipay.Configuration.CardPrivateKeyNotConfigured"] = "No hosted card private key is stored yet.",
            ["Plugins.Payments.Latipay.Configuration.CardPrivateKeyReplaceHint"] = "Leave the private key field blank to keep the existing secret. Enter a new value only when you want to replace it.",
            ["Plugins.Payments.Latipay.Fields.Enabled"] = "Enable payment method",
            ["Plugins.Payments.Latipay.Fields.Enabled.Hint"] = "Toggle whether Latipay is available to customers at checkout. You can keep credentials saved while the method is disabled.",
            ["Plugins.Payments.Latipay.Fields.UseSandbox"] = "Use sandbox / test mode",
            ["Plugins.Payments.Latipay.Fields.UseSandbox.Hint"] = "Toggle this if your Latipay account provides test credentials. The official hosted-online documentation does not publish a sandbox URL, so ensure the API base URL matches the environment you intend to use.",
            ["Plugins.Payments.Latipay.Fields.UserId"] = "User ID",
            ["Plugins.Payments.Latipay.Fields.UserId.Hint"] = "Enter the Latipay user_id value issued for your merchant account.",
            ["Plugins.Payments.Latipay.Fields.UserId.Required"] = "User ID is required when Latipay is enabled.",
            ["Plugins.Payments.Latipay.Fields.UserId.Length"] = "User ID is too long.",
            ["Plugins.Payments.Latipay.Fields.WalletId"] = "Wallet ID",
            ["Plugins.Payments.Latipay.Fields.WalletId.Hint"] = "Enter the Latipay wallet_id value used for hosted payments.",
            ["Plugins.Payments.Latipay.Fields.WalletId.Required"] = "Wallet ID is required when Latipay is enabled.",
            ["Plugins.Payments.Latipay.Fields.WalletId.Length"] = "Wallet ID is too long.",
            ["Plugins.Payments.Latipay.Fields.ApiKey"] = "API key / secret",
            ["Plugins.Payments.Latipay.Fields.ApiKey.Hint"] = "Enter the Latipay shared signing key. For security, the stored secret is never shown again on this page.",
            ["Plugins.Payments.Latipay.Fields.ApiKey.Required"] = "An API key is required when Latipay is enabled unless a secret is already stored.",
            ["Plugins.Payments.Latipay.Fields.ApiKey.Length"] = "API key is too long.",
            ["Plugins.Payments.Latipay.Fields.ApiBaseUrl"] = "API base URL",
            ["Plugins.Payments.Latipay.Fields.ApiBaseUrl.Hint"] = "Use the Latipay API base URL for the environment you intend to call. Leave the official live URL unless Latipay has explicitly provided a different endpoint.",
            ["Plugins.Payments.Latipay.Fields.ApiBaseUrl.Required"] = "API base URL is required.",
            ["Plugins.Payments.Latipay.Fields.ApiBaseUrl.Invalid"] = "API base URL must be a valid absolute HTTPS URL.",
            ["Plugins.Payments.Latipay.Fields.ApiBaseUrl.Length"] = "API base URL is too long.",
            ["Plugins.Payments.Latipay.Fields.CardApiBaseUrl"] = "Hosted card API base URL",
            ["Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Hint"] = "Use the Latipay card-payments API base URL for the environment you intend to call. Leave the official live URL unless Latipay has explicitly provided a different endpoint.",
            ["Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Required"] = "Hosted card API base URL is required when the card payment option is enabled.",
            ["Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Invalid"] = "Hosted card API base URL must be a valid absolute HTTPS URL.",
            ["Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Length"] = "Hosted card API base URL is too long.",
            ["Plugins.Payments.Latipay.Fields.CardMerchantId"] = "Hosted card merchant ID",
            ["Plugins.Payments.Latipay.Fields.CardMerchantId.Hint"] = "Enter the merchant_id value issued by Latipay for hosted card payments.",
            ["Plugins.Payments.Latipay.Fields.CardMerchantId.Required"] = "Hosted card merchant ID is required when the card payment option is enabled.",
            ["Plugins.Payments.Latipay.Fields.CardMerchantId.Length"] = "Hosted card merchant ID is too long.",
            ["Plugins.Payments.Latipay.Fields.CardSiteId"] = "Hosted card site ID",
            ["Plugins.Payments.Latipay.Fields.CardSiteId.Hint"] = "Enter the numeric site_id value issued by Latipay for hosted card payments.",
            ["Plugins.Payments.Latipay.Fields.CardSiteId.Required"] = "Hosted card site ID is required when the card payment option is enabled.",
            ["Plugins.Payments.Latipay.Fields.CardSiteId.Length"] = "Hosted card site ID is too long.",
            ["Plugins.Payments.Latipay.Fields.CardSiteId.Invalid"] = "Hosted card site ID must contain digits only.",
            ["Plugins.Payments.Latipay.Fields.CardPrivateKey"] = "Hosted card private key",
            ["Plugins.Payments.Latipay.Fields.CardPrivateKey.Hint"] = "Enter the private signing key for Latipay hosted card payments. For security, the stored secret is never shown again on this page.",
            ["Plugins.Payments.Latipay.Fields.CardPrivateKey.Required"] = "A hosted card private key is required when the card payment option is enabled unless a secret is already stored.",
            ["Plugins.Payments.Latipay.Fields.CardPrivateKey.Length"] = "Hosted card private key is too long.",
            ["Plugins.Payments.Latipay.Fields.RequestTimeoutSeconds"] = "Request timeout (seconds)",
            ["Plugins.Payments.Latipay.Fields.RequestTimeoutSeconds.Hint"] = "Set the server-to-server API timeout in seconds.",
            ["Plugins.Payments.Latipay.Fields.RequestTimeoutSeconds.Range"] = $"Request timeout must be between {LatipayDefaults.MinRequestTimeoutSeconds} and {LatipayDefaults.MaxRequestTimeoutSeconds} seconds.",
            ["Plugins.Payments.Latipay.Fields.SupportedCurrencyCode"] = "Supported currency",
            ["Plugins.Payments.Latipay.Fields.SupportedCurrencyCode.Hint"] = "Latipay hosted redirect support in this plugin is fixed to NZD only.",
            ["Plugins.Payments.Latipay.Fields.SupportedCurrencyCode.Fixed"] = "Supported currency is fixed to NZD for this plugin.",
            ["Plugins.Payments.Latipay.Fields.DebugLogging"] = "Enable debug logging",
            ["Plugins.Payments.Latipay.Fields.DebugLogging.Hint"] = "Log additional diagnostics for troubleshooting. Sensitive values must remain redacted.",
            ["Plugins.Payments.Latipay.Fields.EnableAlipay"] = "Enable Alipay",
            ["Plugins.Payments.Latipay.Fields.EnableAlipay.Hint"] = "Allow customers to choose Alipay in the Latipay payment step.",
            ["Plugins.Payments.Latipay.Fields.AlipayDisplayName"] = "Alipay display name",
            ["Plugins.Payments.Latipay.Fields.AlipayDisplayName.Hint"] = "Optional custom label shown to customers during checkout and retry flows.",
            ["Plugins.Payments.Latipay.Fields.AlipayDisplayName.Length"] = "Alipay display name is too long.",
            ["Plugins.Payments.Latipay.Fields.AlipayDisplayName.Invalid"] = "Alipay display name cannot contain only whitespace.",
            ["Plugins.Payments.Latipay.Fields.EnableWechatPay"] = "Enable WeChat Pay",
            ["Plugins.Payments.Latipay.Fields.EnableWechatPay.Hint"] = "Allow customers to choose WeChat Pay in the Latipay payment step.",
            ["Plugins.Payments.Latipay.Fields.WechatPayDisplayName"] = "WeChat Pay display name",
            ["Plugins.Payments.Latipay.Fields.WechatPayDisplayName.Hint"] = "Optional custom label shown to customers during checkout and retry flows.",
            ["Plugins.Payments.Latipay.Fields.WechatPayDisplayName.Length"] = "WeChat Pay display name is too long.",
            ["Plugins.Payments.Latipay.Fields.WechatPayDisplayName.Invalid"] = "WeChat Pay display name cannot contain only whitespace.",
            ["Plugins.Payments.Latipay.Fields.EnableNzBanks"] = "Enable NZ Banks",
            ["Plugins.Payments.Latipay.Fields.EnableNzBanks.Hint"] = "Allow customers to choose the New Zealand online banking option exposed by Latipay.",
            ["Plugins.Payments.Latipay.Fields.NzBanksDisplayName"] = "NZ banks display name",
            ["Plugins.Payments.Latipay.Fields.NzBanksDisplayName.Hint"] = "Optional custom label shown to customers during checkout and retry flows.",
            ["Plugins.Payments.Latipay.Fields.NzBanksDisplayName.Length"] = "NZ banks display name is too long.",
            ["Plugins.Payments.Latipay.Fields.NzBanksDisplayName.Invalid"] = "NZ banks display name cannot contain only whitespace.",
            ["Plugins.Payments.Latipay.Fields.EnablePayId"] = "Enable PayID",
            ["Plugins.Payments.Latipay.Fields.EnablePayId.Hint"] = "Allow customers to choose PayID if your Latipay account exposes it for hosted payments.",
            ["Plugins.Payments.Latipay.Fields.PayIdDisplayName"] = "PayID display name",
            ["Plugins.Payments.Latipay.Fields.PayIdDisplayName.Hint"] = "Optional custom label shown to customers during checkout and retry flows.",
            ["Plugins.Payments.Latipay.Fields.PayIdDisplayName.Length"] = "PayID display name is too long.",
            ["Plugins.Payments.Latipay.Fields.PayIdDisplayName.Invalid"] = "PayID display name cannot contain only whitespace.",
            ["Plugins.Payments.Latipay.Fields.EnableUpiUpop"] = "Enable UnionPay UPOP",
            ["Plugins.Payments.Latipay.Fields.EnableUpiUpop.Hint"] = "Allow customers to choose UnionPay UPOP if your Latipay account exposes it for hosted payments.",
            ["Plugins.Payments.Latipay.Fields.UpiUpopDisplayName"] = "UnionPay UPOP display name",
            ["Plugins.Payments.Latipay.Fields.UpiUpopDisplayName.Hint"] = "Optional custom label shown to customers during checkout and retry flows.",
            ["Plugins.Payments.Latipay.Fields.UpiUpopDisplayName.Length"] = "UnionPay UPOP display name is too long.",
            ["Plugins.Payments.Latipay.Fields.UpiUpopDisplayName.Invalid"] = "UnionPay UPOP display name cannot contain only whitespace.",
            ["Plugins.Payments.Latipay.Fields.EnableCardVm"] = "Enable Card (Visa/Mastercard)",
            ["Plugins.Payments.Latipay.Fields.EnableCardVm.Hint"] = "Allow customers to choose Latipay hosted card payments for Visa and Mastercard. This option uses the separate hosted card API and requires billing contact details on the order.",
            ["Plugins.Payments.Latipay.Fields.CardVmDisplayName"] = "Card payment display name",
            ["Plugins.Payments.Latipay.Fields.CardVmDisplayName.Hint"] = "Optional custom label shown to customers during checkout and retry flows for the hosted card option.",
            ["Plugins.Payments.Latipay.Fields.CardVmDisplayName.Length"] = "Card payment display name is too long.",
            ["Plugins.Payments.Latipay.Fields.CardVmDisplayName.Invalid"] = "Card payment display name cannot contain only whitespace.",
            ["Plugins.Payments.Latipay.Fields.EnableRefunds"] = "Enable refunds",
            ["Plugins.Payments.Latipay.Fields.EnableRefunds.Hint"] = "Controls whether the plugin should expose refund actions once refund logic is implemented.",
            ["Plugins.Payments.Latipay.Fields.EnablePartialRefunds"] = "Enable partial refunds",
            ["Plugins.Payments.Latipay.Fields.EnablePartialRefunds.Hint"] = "Allow partial refund requests when Latipay and your merchant account support them.",
            ["Plugins.Payments.Latipay.Fields.EnablePartialRefunds.RequiresRefunds"] = "Partial refunds cannot be enabled unless refunds are enabled.",
            ["Plugins.Payments.Latipay.Fields.EnableReconciliationTask"] = "Enable reconciliation task",
            ["Plugins.Payments.Latipay.Fields.EnableReconciliationTask.Hint"] = "Enable scheduled reconciliation for uncertain payment states.",
            ["Plugins.Payments.Latipay.Fields.ReconciliationPeriodMinutes"] = "Reconciliation period (minutes)",
            ["Plugins.Payments.Latipay.Fields.ReconciliationPeriodMinutes.Hint"] = "Set how often the reconciliation task should run when enabled.",
            ["Plugins.Payments.Latipay.Fields.ReconciliationPeriodMinutes.Range"] = $"Reconciliation period must be between {LatipayDefaults.MinReconciliationPeriodMinutes} and {LatipayDefaults.MaxReconciliationPeriodMinutes} minutes.",
            ["Plugins.Payments.Latipay.Fields.RetryGuardMinutes"] = "Retry guard window (minutes)",
            ["Plugins.Payments.Latipay.Fields.RetryGuardMinutes.Hint"] = "Set the window used to prevent overlapping payment retries on the same order.",
            ["Plugins.Payments.Latipay.Fields.RetryGuardMinutes.Range"] = $"Retry guard window must be between {LatipayDefaults.MinRetryGuardMinutes} and {LatipayDefaults.MaxRetryGuardMinutes} minutes.",
            ["Plugins.Payments.Latipay.Fields.ManualReconcileMerchantReference"] = "Manual recheck merchant reference",
            ["Plugins.Payments.Latipay.Fields.ManualReconcileMerchantReference.Hint"] = "Optional. Enter a Latipay merchant_reference to run a one-off status query.",
            ["Plugins.Payments.Latipay.Fields.ManualReconcileOrderId"] = "Manual recheck order ID",
            ["Plugins.Payments.Latipay.Fields.ManualReconcileOrderId.Hint"] = "Optional. Enter a nopCommerce order ID to reconcile its latest Latipay attempt.",
            ["Plugins.Payments.Latipay.Fields.SubPaymentMethod"] = "Latipay payment option",
            ["Plugins.Payments.Latipay.Fields.SubPaymentMethod.Required"] = "Please select a Latipay payment option.",
            ["Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid"] = "The selected Latipay payment option is not available. Please choose one of the enabled options and try again.",
            ["Plugins.Payments.Latipay.Fields.SubPaymentMethod.AtLeastOne"] = "Enable at least one Latipay sub-payment method before saving an enabled configuration.",
            ["Plugins.Payments.Latipay.ManualReconcile.Hint"] = "Run a one-off Latipay status query by merchant reference or by the latest attempt on a nopCommerce order. This uses the same safe reconciliation rules as callback and browser-return handling.",
            ["Plugins.Payments.Latipay.ManualReconcile.Button"] = "Recheck payment",
            ["Plugins.Payments.Latipay.ManualReconcile.MissingInput"] = "Enter a merchant reference or an order ID before running manual reconciliation.",
            ["Plugins.Payments.Latipay.ManualReconcile.Paid"] = "Latipay reconciliation confirmed the order as paid.",
            ["Plugins.Payments.Latipay.PaymentMethodDescription"] = "Pay with Latipay using a hosted redirect flow.",
            ["Plugins.Payments.Latipay.PaymentInfo.RedirectNotice"] = "After you place the order, you will be redirected to Latipay to complete payment securely.",
            ["Plugins.Payments.Latipay.PaymentInfo.PendingNotice"] = "Payment confirmation can take a moment after you return. Please wait while we confirm the result.",
            ["Plugins.Payments.Latipay.PaymentInfo.CurrencyNotice"] = "Latipay checkout is available for NZD orders only.",
            ["Plugins.Payments.Latipay.PaymentInfo.SingleOptionNotice"] = "Only one Latipay payment option is currently enabled, so it has been preselected for you.",
            ["Plugins.Payments.Latipay.PaymentInfo.NotAvailable"] = "Latipay is not available for checkout right now.",
            ["Plugins.Payments.Latipay.PaymentInfo.CurrencyNotSupported"] = "Latipay checkout is available for NZD orders only.",
            ["Plugins.Payments.Latipay.PaymentInfo.NoAvailableSubPaymentMethods"] = "No Latipay payment options are currently available. Please contact the store owner or choose a different payment method.",
            ["Plugins.Payments.Latipay.Retry.Title"] = "Retry Latipay payment",
            ["Plugins.Payments.Latipay.Retry.OrderLabel"] = "Order:",
            ["Plugins.Payments.Latipay.Return.Pending"] = "Your payment is still being confirmed.",
            ["Plugins.Payments.Latipay.Return.Success"] = "Your payment has been confirmed."
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<LatipaySettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Latipay");

        var scheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(LatipayDefaults.ReconciliationTaskType);
        if (scheduleTask is not null)
            await _scheduleTaskService.DeleteTaskAsync(scheduleTask);

        await base.UninstallAsync();
    }
}
