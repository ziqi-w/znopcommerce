using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.Latipay.Factories;
using Nop.Plugin.Payments.Latipay.Models.Admin;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Latipay.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class LatipayController : BasePaymentController
{
    private const string ConfigureViewPath = "~/Plugins/Payments.Latipay/Views/Admin/Configure.cshtml";

    private readonly LatipayModelFactory _latipayModelFactory;
    private readonly ILatipayReconciliationService _latipayReconciliationService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly ISettingService _settingService;

    public LatipayController(LatipayModelFactory latipayModelFactory,
        ILatipayReconciliationService latipayReconciliationService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService)
    {
        _latipayModelFactory = latipayModelFactory;
        _latipayReconciliationService = latipayReconciliationService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        var model = await _latipayModelFactory.PrepareConfigurationModelAsync();
        return View(ConfigureViewPath, model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!string.Equals(model.SupportedCurrencyCode, LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.SupportedCurrencyCode),
                await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.SupportedCurrencyCode.Fixed"));
        }

        if (!ModelState.IsValid)
        {
            model.ApiKey = string.Empty;
            model.CardPrivateKey = string.Empty;
            var invalidModel = await _latipayModelFactory.PrepareConfigurationModelAsync(model);
            return View(ConfigureViewPath, invalidModel);
        }

        var settings = await _settingService.LoadSettingAsync<LatipaySettings>();
        settings.Enabled = model.Enabled;
        settings.UseSandbox = model.UseSandbox;
        settings.UserId = NormalizeText(model.UserId);
        settings.WalletId = NormalizeText(model.WalletId);
        settings.ApiBaseUrl = NormalizeText(model.ApiBaseUrl);
        settings.CardApiBaseUrl = NormalizeText(model.CardApiBaseUrl);
        settings.CardMerchantId = NormalizeText(model.CardMerchantId);
        settings.CardSiteId = NormalizeText(model.CardSiteId);
        settings.RequestTimeoutSeconds = model.RequestTimeoutSeconds;
        settings.DebugLogging = model.DebugLogging;
        settings.EnableAlipay = model.EnableAlipay;
        settings.AlipayDisplayName = NormalizeOptionalDisplayName(model.AlipayDisplayName);
        settings.EnableWechatPay = model.EnableWechatPay;
        settings.WechatPayDisplayName = NormalizeOptionalDisplayName(model.WechatPayDisplayName);
        settings.EnableNzBanks = model.EnableNzBanks;
        settings.NzBanksDisplayName = NormalizeOptionalDisplayName(model.NzBanksDisplayName);
        settings.EnablePayId = model.EnablePayId;
        settings.PayIdDisplayName = NormalizeOptionalDisplayName(model.PayIdDisplayName);
        settings.EnableUpiUpop = model.EnableUpiUpop;
        settings.UpiUpopDisplayName = NormalizeOptionalDisplayName(model.UpiUpopDisplayName);
        settings.EnableCardVm = model.EnableCardVm;
        settings.CardVmDisplayName = NormalizeOptionalDisplayName(model.CardVmDisplayName);
        settings.EnableRefunds = model.EnableRefunds;
        settings.EnablePartialRefunds = model.EnablePartialRefunds;
        settings.EnableReconciliationTask = model.EnableReconciliationTask;
        settings.ReconciliationPeriodMinutes = model.ReconciliationPeriodMinutes;
        settings.RetryGuardMinutes = model.RetryGuardMinutes;

        if (!string.IsNullOrWhiteSpace(model.ApiKey))
            settings.ApiKey = NormalizeSecret(model.ApiKey);

        if (!string.IsNullOrWhiteSpace(model.CardPrivateKey))
            settings.CardPrivateKey = NormalizeSecret(model.CardPrivateKey);

        // TODO: Wire the reconciliation task enablement and period updates.
        await _settingService.SaveSettingAsync(settings);
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
        return await Configure();
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> ManualReconcile(ConfigurationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var merchantReference = NormalizeText(model.ManualReconcileMerchantReference);
        var orderId = model.ManualReconcileOrderId.GetValueOrDefault();

        if (string.IsNullOrWhiteSpace(merchantReference) && orderId <= 0)
        {
            _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.ManualReconcile.MissingInput"));
            return await Configure();
        }

        var result = !string.IsNullOrWhiteSpace(merchantReference)
            ? await _latipayReconciliationService.ReconcileByMerchantReferenceAsync(merchantReference, "manual admin reconciliation")
            : await _latipayReconciliationService.ReconcileLatestAttemptForOrderAsync(orderId, "manual admin reconciliation");

        if (result.IsPaid)
        {
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Payments.Latipay.ManualReconcile.Paid"));
        }
        else if (result.ReviewRequired)
        {
            _notificationService.WarningNotification(result.Message);
        }
        else
        {
            _notificationService.SuccessNotification(result.Message);
        }

        return await Configure();
    }

    private static string NormalizeOptionalDisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string NormalizeSecret(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
