using Nop.Core;

namespace WS.Plugin.Payments.Latipay;

/// <summary>
/// Represents plugin constants.
/// </summary>
public static class LatipayDefaults
{
    // Keep the nopCommerce payment-method identity stable so existing orders and plugin registrations still resolve this payment method.
    public static string SystemName => "Payments.Latipay";

    public static string LegacyReconciliationTaskType =>
        "Nop.Plugin.Payments.Latipay.Services.LatipayReconciliationTask, Nop.Plugin.Payments.Latipay";

    public static string ApiBaseUrl => "https://api.latipay.net/";

    public static string CardApiBaseUrl => "https://latipay-gateway.gigsign.com/";

    public static string DefaultAlipayDisplayName => "Alipay";

    public static string DefaultWechatPayDisplayName => "WeChat Pay";

    public static string DefaultNzBanksDisplayName => "NZ bank transfer";

    public static string DefaultPayIdDisplayName => "PayID";

    public static string DefaultUpiUpopDisplayName => "UnionPay UPOP";

    public static string DefaultCardVmDisplayName => "Card (Visa/Mastercard)";

    public static string CurrencyCode => "NZD";

    public static string ApiVersion => "2.0";

    public static string UserAgent => $"nopCommerce-{NopVersion.FULL_VERSION}";

    public static string SelectedMethodCustomValueKey => "Latipay.SelectedSubPaymentMethod";

    public static string SelectedMethodProviderValueCustomValueKey => "Latipay.SelectedProviderSubPaymentMethod";

    public static string SelectedMethodDisplayCustomValueKey => "Latipay.SelectedSubPaymentMethodDisplay";

    public static string ReconciliationTaskName => "Latipay payment reconciliation";

    public static string ReconciliationTaskType =>
        "WS.Plugin.Payments.Latipay.Services.LatipayReconciliationTask, WS.Plugin.Payments.Latipay";

    public static int DefaultRequestTimeoutSeconds => 15;

    public static int DefaultReconciliationPeriodMinutes => 5;

    public static int DefaultRetryGuardMinutes => 10;

    public static int MinRequestTimeoutSeconds => 5;

    public static int MaxRequestTimeoutSeconds => 300;

    public static int MinReconciliationPeriodMinutes => 1;

    public static int MaxReconciliationPeriodMinutes => 1440;

    public static int MinRetryGuardMinutes => 1;

    public static int MaxRetryGuardMinutes => 1440;

    public static int MaxCredentialLength => 512;

    public static int MaxDisplayNameLength => 100;

    public static int MaxApiBaseUrlLength => 500;

    public static int MaxCardSiteIdLength => 20;

    public static class ApiPaths
    {
        public static string Transaction => "v2/transaction";

        public static string Refund => "refund";

        public static string CardTransaction => "cardpayments";

        public static string CardQueryTransaction => "2023-04/transaction/query";

        public static string CardRefund => "2023-04/refund";
    }

    public static class SubPaymentMethodKeys
    {
        public static string Alipay => "Alipay";

        public static string WechatPay => "WechatPay";

        public static string NzBanks => "NzBanks";

        public static string PayId => "PayId";

        public static string UpiUpop => "UpiUpop";

        public static string CardVm => "CardVm";
    }

    public static class ProviderSubPaymentMethodValues
    {
        public static string Alipay => "alipay";

        public static string WechatPay => "wechat";

        public static string NzBanks => "polipay";

        public static string NzBanksReturnAlias => "onlineBank";

        public static string PayId => "payid";

        public static string UpiUpop => "upi_upop";

        public static string CardVm => "vm";
    }

    public static class Route
    {
        public static string Configuration => "Plugin.Payments.Latipay.Configure";

        public static string ManualReconcile => "Plugin.Payments.Latipay.ManualReconcile";

        public static string Retry => "Plugin.Payments.Latipay.Retry";

        public static string Return => "Plugin.Payments.Latipay.Return";

        public static string Callback => "Plugin.Payments.Latipay.Callback";
    }

    public static class Content
    {
        public static string RootPath => "~/Plugins/Payments.Latipay/Content";

        public static string PublicStylesheetPath => $"{RootPath}/css/latipay-public.css";

        public static string PaymentOptionLogoFolderPath => $"{RootPath}/images/payment-options";
    }
}
