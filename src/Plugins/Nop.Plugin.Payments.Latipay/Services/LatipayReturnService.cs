using Nop.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.Latipay.Services;

/// <summary>
/// Handles browser return processing without trusting browser state as final payment proof.
/// </summary>
public class LatipayReturnService : ILatipayReturnService
{
    private readonly ILogger _logger;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipayReconciliationService _latipayReconciliationService;
    private readonly ILatipaySignatureService _latipaySignatureService;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly LatipaySettings _settings;

    public LatipayReturnService(ILogger logger,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipayReconciliationService latipayReconciliationService,
        ILatipaySignatureService latipaySignatureService,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        LatipaySettings settings)
    {
        _logger = logger;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipayReconciliationService = latipayReconciliationService;
        _latipaySignatureService = latipaySignatureService;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _settings = settings;
    }

    public async Task<LatipayReturnProcessResult> ProcessReturnAsync(LatipayStatusNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var merchantReference = NormalizeOptional(notification.MerchantReference);
        if (string.IsNullOrWhiteSpace(merchantReference))
        {
            await _logger.WarningAsync("Latipay browser return was received without a merchant reference.");
            return new LatipayReturnProcessResult
            {
                Message = "We received your return from Latipay, but the payment reference was missing. Your order remains pending while we wait for verified confirmation."
            };
        }

        var paymentAttempt = await _latipayPaymentAttemptService.GetByMerchantReferenceAsync(merchantReference);
        var isHostedCardAttempt = paymentAttempt is not null
            && _latipaySubPaymentMethodService.TryGetMethod(paymentAttempt.SelectedSubPaymentMethod, out var selectedMethod)
            && selectedMethod.IntegrationMode == Domain.Enums.LatipayIntegrationMode.HostedCard;

        var canValidateSignature = !isHostedCardAttempt
            && notification.HasSignatureFields
            && !string.IsNullOrWhiteSpace(_settings.ApiKey);
        var browserSignatureValid = canValidateSignature
            && _latipaySignatureService.IsStatusSignatureValid(
                notification.MerchantReference,
                notification.PaymentMethod,
                notification.Status,
                notification.Currency,
                notification.Amount,
                notification.Signature,
                _settings.ApiKey);

        if (notification.HasSignatureFields && !browserSignatureValid)
        {
            await _logger.WarningAsync(
                $"Latipay browser return signature validation failed for merchant reference '{merchantReference}'. A server-side reconciliation will be attempted.");
        }

        var reconciliationResult = await _latipayReconciliationService.ReconcileByMerchantReferenceAsync(
            merchantReference,
            "browser return reconciliation",
            cancellationToken);

        return new LatipayReturnProcessResult
        {
            IsConfirmedPaid = reconciliationResult.IsPaid,
            OrderId = reconciliationResult.OrderId,
            MerchantReference = merchantReference,
            Status = NormalizeOptional(notification.Status) ?? reconciliationResult.ExternalStatus,
            Message = BuildMessage(browserSignatureValid, canValidateSignature, reconciliationResult)
        };
    }

    private static string BuildMessage(bool browserSignatureValid, bool attemptedBrowserSignatureValidation, LatipayReconciliationResult reconciliationResult)
    {
        ArgumentNullException.ThrowIfNull(reconciliationResult);

        if (reconciliationResult.IsPaid)
            return "Your payment was verified with Latipay and the order has been confirmed.";

        if (reconciliationResult.ReviewRequired)
        {
            return attemptedBrowserSignatureValidation && !browserSignatureValid
                ? "We could not trust the browser return details, and the payment could not be confirmed automatically. Your order remains pending for review."
                : "Your payment could not be confirmed automatically. Your order remains pending while we review the Latipay status.";
        }

        if (attemptedBrowserSignatureValidation && !browserSignatureValid)
            return "We could not trust the browser return details, so we checked Latipay directly. Your order remains pending while payment confirmation continues.";

        return "We are still confirming your payment with Latipay. Your order remains pending until verified confirmation is received.";
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
