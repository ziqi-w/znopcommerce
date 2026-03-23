using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Components.Public;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Models.Public;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Validators;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.ScheduleTasks;
using Nop.Web.Framework.Mvc.Routing;

namespace WS.Plugin.Payments.Latipay;

/// <summary>
/// Represents the Latipay hosted payment method scaffold.
/// </summary>
public class LatipayPaymentMethod : LatipayPlugin, IPaymentMethod
{
    private readonly ILatipayCheckoutService _latipayCheckoutService;
    private readonly ILocalizationService _localizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipayRefundService _latipayRefundService;
    private readonly ILatipayRetryEligibilityService _latipayRetryEligibilityService;
    private readonly LatipaySettings _settings;
    private readonly ILatipaySubPaymentMethodService _subPaymentMethodService;
    private readonly IWorkContext _workContext;

    public LatipayPaymentMethod(ILatipayCheckoutService latipayCheckoutService,
        ILocalizationService localizationService,
        IHttpContextAccessor httpContextAccessor,
        INopUrlHelper nopUrlHelper,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipayRefundService latipayRefundService,
        ILatipayRetryEligibilityService latipayRetryEligibilityService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        ILatipaySubPaymentMethodService subPaymentMethodService,
        IWorkContext workContext,
        LatipaySettings settings)
        : base(localizationService, nopUrlHelper, scheduleTaskService, settingService)
    {
        _latipayCheckoutService = latipayCheckoutService;
        _localizationService = localizationService;
        _httpContextAccessor = httpContextAccessor;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipayRefundService = latipayRefundService;
        _latipayRetryEligibilityService = latipayRetryEligibilityService;
        _subPaymentMethodService = subPaymentMethodService;
        _settings = settings;
        _workContext = workContext;
    }

    public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);

        var selectionKey = GetCustomValue(processPaymentRequest.CustomValues, LatipayDefaults.SelectedMethodCustomValueKey);
        var selectionWarnings = await GetPaymentSelectionWarningsAsync(selectionKey);
        if (selectionWarnings.Count > 0)
        {
            return new ProcessPaymentResult
            {
                Errors = selectionWarnings.ToArray()
            };
        }

        if (!_subPaymentMethodService.TryGetEnabledMethod(_settings, selectionKey, out var selectedMethod))
        {
            return new ProcessPaymentResult
            {
                Errors = [await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid")]
            };
        }

        var storedProviderValue = GetCustomValue(processPaymentRequest.CustomValues, LatipayDefaults.SelectedMethodProviderValueCustomValueKey);
        if (!string.IsNullOrWhiteSpace(storedProviderValue)
            && !storedProviderValue.Equals(selectedMethod.ProviderValue, StringComparison.Ordinal))
        {
            return new ProcessPaymentResult
            {
                Errors = [await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid")]
            };
        }

        StoreSelectedSubPaymentMethod(processPaymentRequest, selectedMethod);
        return new ProcessPaymentResult
        {
            NewPaymentStatus = PaymentStatus.Pending
        };
    }

    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(postProcessPaymentRequest);
        ArgumentNullException.ThrowIfNull(postProcessPaymentRequest.Order);

        if (postProcessPaymentRequest.Order.PaymentStatus == PaymentStatus.Paid)
            return;

        var order = postProcessPaymentRequest.Order;

        string redirectUrl;
        if (IsOrderDetailsRetryRequest())
        {
            redirectUrl = GetRouteUrl(LatipayDefaults.Route.Retry, new { orderId = order.Id });
        }
        else
        {
            var latestAttempt = await _latipayPaymentAttemptService.GetLatestByOrderIdAsync(order.Id);
            var selectedMethodKey = GetStoredSelectedMethodKey(order);
            if (latestAttempt is null && !string.IsNullOrWhiteSpace(selectedMethodKey))
            {
                var startResult = await _latipayCheckoutService.StartHostedPaymentAsync(order.Id, selectedMethodKey);
                redirectUrl = startResult.Started && !string.IsNullOrWhiteSpace(startResult.HostedPaymentUrl)
                    ? startResult.HostedPaymentUrl
                    : GetRouteUrl(LatipayDefaults.Route.Retry, new { orderId = order.Id, message = startResult.Message });
            }
            else
            {
                redirectUrl = GetRouteUrl(LatipayDefaults.Route.Retry, new { orderId = order.Id });
            }
        }

        if (!string.IsNullOrWhiteSpace(redirectUrl))
            _httpContextAccessor.HttpContext?.Response.Redirect(redirectUrl);
    }

    public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        return !_settings.Enabled
            || !_subPaymentMethodService.HasAnyEnabledMethods(_settings)
            || !await IsWorkingCurrencySupportedAsync();
    }

    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(decimal.Zero);
    }

    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult
        {
            Errors = ["Capture method not supported for the Latipay hosted redirect flow."]
        });
    }

    public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(refundPaymentRequest);

        return await _latipayRefundService.RefundAsync(refundPaymentRequest);
    }

    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult
        {
            Errors = ["Void method not supported for the Latipay hosted redirect flow."]
        });
    }

    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult
        {
            Errors = ["Recurring billing is not supported by the Latipay hosted redirect flow."],
            RecurringPaymentFailed = true
        });
    }

    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult
        {
            Errors = ["Recurring billing is not supported by the Latipay hosted redirect flow."]
        });
    }

    public async Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return await _latipayRetryEligibilityService.CanRetryAsync(order);
    }

    public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        var enabledMethods = _subPaymentMethodService.GetEnabledMethods(_settings);
        var model = new PaymentInfoModel
        {
            SelectedSubPaymentMethod = NormalizeSelectionKey(form[nameof(PaymentInfoModel.SelectedSubPaymentMethod)]),
            AvailableSubPaymentMethods = enabledMethods
                .Select(method => new LatipaySubPaymentMethodModel
                {
                    Key = method.Key,
                    DisplayName = method.DisplayName,
                    LogoUrl = method.LogoUrl
                })
                .ToList()
        };
        var validator = new PaymentInfoValidator(_localizationService,
            enabledMethods.Select(method => method.Key),
            _settings.Enabled,
            await IsWorkingCurrencySupportedAsync());

        var validationResult = validator.Validate(model);
        return validationResult.IsValid
            ? []
            : validationResult.Errors.Select(error => error.ErrorMessage).ToList();
    }

    public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        var selectedMethodKey = NormalizeSelectionKey(form[nameof(PaymentInfoModel.SelectedSubPaymentMethod)]);
        var selectionWarnings = await GetPaymentSelectionWarningsAsync(selectedMethodKey);
        if (selectionWarnings.Count > 0)
            throw new InvalidOperationException(string.Join(" ", selectionWarnings));

        if (!_subPaymentMethodService.TryGetEnabledMethod(_settings, selectedMethodKey, out var selectedMethod))
            throw new InvalidOperationException(await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid"));

        var request = new ProcessPaymentRequest();
        StoreSelectedSubPaymentMethod(request, selectedMethod);

        return request;
    }

    public Type GetPublicViewComponent()
    {
        return typeof(PaymentInfoViewComponent);
    }

    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.PaymentMethodDescription");
    }

    public bool SupportCapture => false;

    public bool SupportPartiallyRefund => _settings.EnableRefunds && _settings.EnablePartialRefunds;

    public bool SupportRefund => _settings.EnableRefunds;

    public bool SupportVoid => false;

    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    public bool SkipPaymentInfo => false;

    private static string GetCustomValue(CustomValues customValues, string key)
    {
        return customValues is not null
               && customValues.TryGetValue(key, out var value)
            ? value.Value
            : string.Empty;
    }

    private string GetStoredSelectedMethodKey(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var customValues = new CustomValues();
        customValues.FillByXml(order.CustomValuesXml);

        return NormalizeSelectionKey(GetCustomValue(customValues, LatipayDefaults.SelectedMethodCustomValueKey));
    }

    private bool IsOrderDetailsRetryRequest()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        return request is not null
            && request.HasFormContentType
            && request.Form.ContainsKey("repost-payment");
    }

    private static string NormalizeSelectionKey(string selectionKey)
    {
        return string.IsNullOrWhiteSpace(selectionKey)
            ? string.Empty
            : selectionKey.Trim();
    }

    private async Task<IList<string>> GetPaymentSelectionWarningsAsync(string selectionKey)
    {
        var enabledMethods = _subPaymentMethodService.GetEnabledMethods(_settings);
        var model = new PaymentInfoModel
        {
            SelectedSubPaymentMethod = NormalizeSelectionKey(selectionKey),
            AvailableSubPaymentMethods = enabledMethods
                .Select(method => new LatipaySubPaymentMethodModel
                {
                    Key = method.Key,
                    DisplayName = method.DisplayName,
                    LogoUrl = method.LogoUrl
                })
                .ToList()
        };
        var validator = new PaymentInfoValidator(_localizationService,
            enabledMethods.Select(method => method.Key),
            _settings.Enabled,
            await IsWorkingCurrencySupportedAsync());
        var validationResult = validator.Validate(model);

        return validationResult.IsValid
            ? []
            : validationResult.Errors.Select(error => error.ErrorMessage).ToList();
    }

    private async Task<bool> IsWorkingCurrencySupportedAsync()
    {
        var workingCurrency = await _workContext.GetWorkingCurrencyAsync();
        return string.Equals(workingCurrency?.CurrencyCode, LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase);
    }

    private static void StoreSelectedSubPaymentMethod(ProcessPaymentRequest processPaymentRequest, LatipaySubPaymentMethodOption selectedMethod)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);
        ArgumentNullException.ThrowIfNull(selectedMethod);

        processPaymentRequest.CustomValues.Remove(LatipayDefaults.SelectedMethodCustomValueKey);
        processPaymentRequest.CustomValues.Remove(LatipayDefaults.SelectedMethodProviderValueCustomValueKey);
        processPaymentRequest.CustomValues.Remove(LatipayDefaults.SelectedMethodDisplayCustomValueKey);

        processPaymentRequest.CustomValues.Add(new CustomValue(
            LatipayDefaults.SelectedMethodCustomValueKey,
            selectedMethod.Key,
            displayToCustomer: false));
        processPaymentRequest.CustomValues.Add(new CustomValue(
            LatipayDefaults.SelectedMethodProviderValueCustomValueKey,
            selectedMethod.ProviderValue,
            displayToCustomer: false));
        processPaymentRequest.CustomValues.Add(new CustomValue(
            LatipayDefaults.SelectedMethodDisplayCustomValueKey,
            selectedMethod.DisplayName,
            displayToCustomer: false));
    }
}
