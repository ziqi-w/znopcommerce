using Nop.Plugin.Payments.Latipay.Domain;
using Nop.Plugin.Payments.Latipay.Models.Admin;
using Nop.Plugin.Payments.Latipay.Models.Public;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Core;

namespace Nop.Plugin.Payments.Latipay.Factories;

/// <summary>
/// Prepares models for the Latipay scaffold.
/// </summary>
public class LatipayModelFactory
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ISettingService _settingService;
    private readonly ILatipaySubPaymentMethodService _subPaymentMethodService;
    private readonly IStoreContext _storeContext;

    public LatipayModelFactory(IOrderProcessingService orderProcessingService,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ISettingService settingService,
        ILatipaySubPaymentMethodService subPaymentMethodService,
        IStoreContext storeContext)
    {
        _orderProcessingService = orderProcessingService;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _settingService = settingService;
        _subPaymentMethodService = subPaymentMethodService;
        _storeContext = storeContext;
    }

    public async Task<ConfigurationModel> PrepareConfigurationModelAsync()
    {
        var settings = await _settingService.LoadSettingAsync<LatipaySettings>();
        var model = new ConfigurationModel
        {
            ActiveStoreScopeConfiguration = await _storeContext.GetActiveStoreScopeConfigurationAsync(),
            Enabled = settings.Enabled,
            UseSandbox = settings.UseSandbox,
            UserId = settings.UserId,
            WalletId = settings.WalletId,
            ApiBaseUrl = settings.ApiBaseUrl,
            CardApiBaseUrl = settings.CardApiBaseUrl,
            CardMerchantId = settings.CardMerchantId,
            CardSiteId = settings.CardSiteId,
            RequestTimeoutSeconds = settings.RequestTimeoutSeconds,
            SupportedCurrencyCode = LatipayDefaults.CurrencyCode,
            DebugLogging = settings.DebugLogging,
            EnableAlipay = settings.EnableAlipay,
            AlipayDisplayName = settings.AlipayDisplayName,
            EnableWechatPay = settings.EnableWechatPay,
            WechatPayDisplayName = settings.WechatPayDisplayName,
            EnableNzBanks = settings.EnableNzBanks,
            NzBanksDisplayName = settings.NzBanksDisplayName,
            EnablePayId = settings.EnablePayId,
            PayIdDisplayName = settings.PayIdDisplayName,
            EnableUpiUpop = settings.EnableUpiUpop,
            UpiUpopDisplayName = settings.UpiUpopDisplayName,
            EnableCardVm = settings.EnableCardVm,
            CardVmDisplayName = settings.CardVmDisplayName,
            EnableRefunds = settings.EnableRefunds,
            EnablePartialRefunds = settings.EnablePartialRefunds,
            EnableReconciliationTask = settings.EnableReconciliationTask,
            ReconciliationPeriodMinutes = settings.ReconciliationPeriodMinutes,
            RetryGuardMinutes = settings.RetryGuardMinutes,
            ApiKeyConfigured = !string.IsNullOrWhiteSpace(settings.ApiKey),
            CardPrivateKeyConfigured = !string.IsNullOrWhiteSpace(settings.CardPrivateKey)
        };

        return model;
    }

    public async Task<ConfigurationModel> PrepareConfigurationModelAsync(ConfigurationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var settings = await _settingService.LoadSettingAsync<LatipaySettings>();

        model.ActiveStoreScopeConfiguration = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        model.ApiKeyConfigured = !string.IsNullOrWhiteSpace(settings.ApiKey);
        model.CardPrivateKeyConfigured = !string.IsNullOrWhiteSpace(settings.CardPrivateKey);
        model.SupportedCurrencyCode = LatipayDefaults.CurrencyCode;

        return model;
    }

    public async Task<PaymentInfoModel> PreparePaymentInfoModelAsync()
    {
        var settings = await _settingService.LoadSettingAsync<LatipaySettings>();
        var selectedSubPaymentMethod = await GetStoredSelectedSubPaymentMethodAsync();
        var availableMethods = _subPaymentMethodService.GetEnabledMethods(settings);
        var model = new PaymentInfoModel
        {
            AvailableSubPaymentMethods = PrepareSubPaymentMethodList(availableMethods)
        };

        model.SelectedSubPaymentMethod = ResolveSelectedSubPaymentMethod(selectedSubPaymentMethod, availableMethods);
        return model;
    }

    public async Task<RetryPaymentModel> PrepareRetryPaymentModelAsync(int orderId,
        string orderNumber = null,
        string message = null,
        string selectedSubPaymentMethod = null,
        bool canRetry = true)
    {
        var settings = await _settingService.LoadSettingAsync<LatipaySettings>();
        var availableMethods = _subPaymentMethodService.GetEnabledMethods(settings);
        var latestAttempt = await _latipayPaymentAttemptService.GetLatestByOrderIdAsync(orderId);
        var model = new RetryPaymentModel
        {
            OrderId = orderId,
            OrderNumber = orderNumber,
            Message = message,
            CanRetry = canRetry,
            AvailableSubPaymentMethods = PrepareSubPaymentMethodList(availableMethods)
        };

        model.SelectedSubPaymentMethod = ResolveSelectedSubPaymentMethod(
            selectedSubPaymentMethod ?? latestAttempt?.SelectedSubPaymentMethod,
            availableMethods);
        return model;
    }

    public Task<ReturnStatusModel> PrepareReturnStatusModelAsync(string merchantReference, string status, string message)
    {
        return Task.FromResult(new ReturnStatusModel
        {
            MerchantReference = merchantReference,
            Status = status,
            Message = message
        });
    }

    protected virtual IList<LatipaySubPaymentMethodModel> PrepareSubPaymentMethodList(IReadOnlyList<LatipaySubPaymentMethodOption> availableMethods)
    {
        ArgumentNullException.ThrowIfNull(availableMethods);

        return availableMethods
            .Select(method => new LatipaySubPaymentMethodModel
            {
                Key = method.Key,
                DisplayName = method.DisplayName,
                LogoUrl = method.LogoUrl
            })
            .ToList();
    }

    protected virtual string ResolveSelectedSubPaymentMethod(string selectionKey, IReadOnlyList<LatipaySubPaymentMethodOption> availableMethods)
    {
        ArgumentNullException.ThrowIfNull(availableMethods);

        if (!string.IsNullOrWhiteSpace(selectionKey))
        {
            var matchingMethod = availableMethods.FirstOrDefault(method =>
                method.Key.Equals(selectionKey.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchingMethod is not null)
                return matchingMethod.Key;
        }

        return availableMethods.Count == 1
            ? availableMethods[0].Key
            : string.Empty;
    }

    protected virtual async Task<string> GetStoredSelectedSubPaymentMethodAsync()
    {
        var processPaymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync();

        return processPaymentRequest?.CustomValues is not null
               && processPaymentRequest.CustomValues.TryGetValue(LatipayDefaults.SelectedMethodCustomValueKey, out var customValue)
            ? customValue.Value
            : string.Empty;
    }
}
