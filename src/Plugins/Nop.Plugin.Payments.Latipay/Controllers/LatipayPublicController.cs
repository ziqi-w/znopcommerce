using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Nop.Plugin.Payments.Latipay.Factories;
using Nop.Plugin.Payments.Latipay.Models.Public;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Controllers;

namespace Nop.Plugin.Payments.Latipay.Controllers;

/// <summary>
/// Hosts public retry, return, and callback endpoints for Latipay.
/// </summary>
public class LatipayPublicController : BasePublicController
{
    private readonly LatipayModelFactory _latipayModelFactory;
    private readonly ILatipayCallbackService _latipayCallbackService;
    private readonly ILatipayCheckoutService _latipayCheckoutService;
    private readonly ILocalizationService _localizationService;
    private readonly ILatipayRetryEligibilityService _latipayRetryEligibilityService;
    private readonly ILatipayReturnService _latipayReturnService;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly IOrderService _orderService;
    private readonly IWorkContext _workContext;
    private readonly LatipaySettings _settings;

    public LatipayPublicController(LatipayModelFactory latipayModelFactory,
        ILatipayCallbackService latipayCallbackService,
        ILatipayCheckoutService latipayCheckoutService,
        ILocalizationService localizationService,
        ILatipayRetryEligibilityService latipayRetryEligibilityService,
        ILatipayReturnService latipayReturnService,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        IOrderService orderService,
        IWorkContext workContext,
        LatipaySettings settings)
    {
        _latipayModelFactory = latipayModelFactory;
        _latipayCallbackService = latipayCallbackService;
        _latipayCheckoutService = latipayCheckoutService;
        _localizationService = localizationService;
        _latipayRetryEligibilityService = latipayRetryEligibilityService;
        _latipayReturnService = latipayReturnService;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _orderService = orderService;
        _workContext = workContext;
        _settings = settings;
    }

    public async Task<IActionResult> Retry(int orderId, string message = null)
    {
        var order = await GetOwnedOrderAsync(orderId);
        if (order is null)
            return Challenge();

        var eligibility = await _latipayRetryEligibilityService.EvaluateAsync(order);
        var effectiveMessage = string.IsNullOrWhiteSpace(message)
            ? eligibility.Message
            : message;

        var model = await _latipayModelFactory.PrepareRetryPaymentModelAsync(
            order.Id,
            order.CustomOrderNumber,
            effectiveMessage,
            canRetry: eligibility.CanRetry);
        return View("~/Plugins/Payments.Latipay/Views/Public/Retry.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(RetryPaymentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var order = await GetOwnedOrderAsync(model.OrderId);
        if (order is null)
            return Challenge();

        var eligibility = await _latipayRetryEligibilityService.EvaluateAsync(order);
        if (!eligibility.CanRetry)
        {
            var blockedModel = await _latipayModelFactory.PrepareRetryPaymentModelAsync(
                order.Id,
                order.CustomOrderNumber,
                eligibility.Message,
                model.SelectedSubPaymentMethod,
                canRetry: false);
            return View("~/Plugins/Payments.Latipay/Views/Public/Retry.cshtml", blockedModel);
        }

        if (!IsRetrySelectionValid(model.SelectedSubPaymentMethod, out var validationResourceKey))
        {
            ModelState.AddModelError(
                nameof(model.SelectedSubPaymentMethod),
                await _localizationService.GetResourceAsync(validationResourceKey));
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await _latipayModelFactory.PrepareRetryPaymentModelAsync(
                order.Id,
                order.CustomOrderNumber,
                eligibility.Message,
                model.SelectedSubPaymentMethod,
                canRetry: true);
            return View("~/Plugins/Payments.Latipay/Views/Public/Retry.cshtml", invalidModel);
        }

        var startResult = await _latipayCheckoutService.StartHostedPaymentAsync(order.Id, model.SelectedSubPaymentMethod);
        if (startResult.Started && !string.IsNullOrWhiteSpace(startResult.HostedPaymentUrl))
            return Redirect(startResult.HostedPaymentUrl);

        var refreshedEligibility = await _latipayRetryEligibilityService.EvaluateAsync(order);
        var retryModel = await _latipayModelFactory.PrepareRetryPaymentModelAsync(
            order.Id,
            order.CustomOrderNumber,
            startResult.Message,
            model.SelectedSubPaymentMethod,
            canRetry: refreshedEligibility.CanRetry);
        return View("~/Plugins/Payments.Latipay/Views/Public/Retry.cshtml", retryModel);
    }

    public async Task<IActionResult> Return()
    {
        var result = await _latipayReturnService.ProcessReturnAsync(BuildStatusNotification(Request.Query));
        var model = await _latipayModelFactory.PrepareReturnStatusModelAsync(
            result.MerchantReference,
            result.Status,
            result.Message);

        var viewPath = result.IsConfirmedPaid
            ? "~/Plugins/Payments.Latipay/Views/Public/ReturnSuccess.cshtml"
            : "~/Plugins/Payments.Latipay/Views/Public/ReturnPending.cshtml";

        return View(viewPath, model);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback()
    {
        var notification = Request.HasFormContentType
            ? BuildStatusNotification(await Request.ReadFormAsync())
            : new LatipayStatusNotification();

        var result = await _latipayCallbackService.ProcessCallbackAsync(notification);
        if (result.AcknowledgeCallback)
            return Content("sent");

        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private async Task<Nop.Core.Domain.Orders.Order> GetOwnedOrderAsync(int orderId)
    {
        if (orderId <= 0)
            return null;

        var order = await _orderService.GetOrderByIdAsync(orderId);
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (order is null || order.Deleted || customer is null || customer.Id != order.CustomerId)
            return null;

        return order;
    }

    private static LatipayStatusNotification BuildStatusNotification(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        return new LatipayStatusNotification
        {
            MerchantReference = GetFirstValue(form, "merchant_reference", "out_trade_no"),
            PaymentMethod = GetValue(form, "payment_method"),
            NotifyVersion = GetValue(form, "notify_version"),
            Status = GetValue(form, "status"),
            Currency = GetValue(form, "currency"),
            Amount = GetValue(form, "amount"),
            OrderId = GetValue(form, "order_id"),
            PayTime = GetValue(form, "pay_time"),
            Signature = GetValue(form, "signature")
        };
    }

    private static LatipayStatusNotification BuildStatusNotification(IQueryCollection query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new LatipayStatusNotification
        {
            MerchantReference = GetFirstValue(query, "merchant_reference", "out_trade_no"),
            PaymentMethod = GetValue(query, "payment_method"),
            NotifyVersion = GetValue(query, "notify_version"),
            Status = GetValue(query, "status"),
            Currency = GetValue(query, "currency"),
            Amount = GetValue(query, "amount"),
            OrderId = GetValue(query, "order_id"),
            PayTime = GetValue(query, "pay_time"),
            Signature = GetValue(query, "signature")
        };
    }

    private static string GetValue(IQueryCollection values, string key)
    {
        return values.TryGetValue(key, out StringValues value)
            ? NormalizeOptional(value.ToString())
            : null;
    }

    private static string GetFirstValue(IQueryCollection values, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetValue(values, key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string GetValue(IFormCollection values, string key)
    {
        return values.TryGetValue(key, out StringValues value)
            ? NormalizeOptional(value.ToString())
            : null;
    }

    private static string GetFirstValue(IFormCollection values, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetValue(values, key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private bool IsRetrySelectionValid(string selectionKey, out string validationResourceKey)
    {
        if (string.IsNullOrWhiteSpace(selectionKey))
        {
            validationResourceKey = "Plugins.Payments.Latipay.Fields.SubPaymentMethod.Required";
            return false;
        }

        validationResourceKey = "Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid";
        return _latipaySubPaymentMethodService.TryGetEnabledMethod(_settings, selectionKey, out _);
    }
}
