using FluentValidation;
using FluentValidation.Results;
using Nop.Plugin.Payments.Latipay.Models.Public;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.Latipay.Validators;

/// <summary>
/// Validates the checkout payment selection using runtime plugin state.
/// </summary>
public class PaymentInfoValidator
{
    private readonly InlineValidator<PaymentInfoModel> _validator = new();

    public PaymentInfoValidator(ILocalizationService localizationService,
        IEnumerable<string> availableSelectionKeys,
        bool paymentMethodEnabled,
        bool currencySupported)
    {
        var enabledSelectionKeys = new HashSet<string>(
            (availableSelectionKeys ?? Enumerable.Empty<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim()),
            StringComparer.OrdinalIgnoreCase);

        // This validator intentionally is not registered as a DI-discoverable FluentValidation validator.
        // Its rules depend on runtime state such as enabled methods and the active currency.
        _validator.RuleFor(model => model)
            .Must(_ => paymentMethodEnabled)
            .WithMessage(GetResource(localizationService, "Plugins.Payments.Latipay.PaymentInfo.NotAvailable"));

        _validator.RuleFor(model => model)
            .Must(_ => currencySupported)
            .WithMessage(GetResource(localizationService, "Plugins.Payments.Latipay.PaymentInfo.CurrencyNotSupported"));

        _validator.RuleFor(model => model)
            .Must(_ => enabledSelectionKeys.Count > 0)
            .WithMessage(GetResource(localizationService, "Plugins.Payments.Latipay.PaymentInfo.NoAvailableSubPaymentMethods"));

        _validator.RuleFor(model => model.SelectedSubPaymentMethod)
            .NotEmpty()
            .When(_ => paymentMethodEnabled && currencySupported && enabledSelectionKeys.Count > 0)
            .WithMessage(GetResource(localizationService, "Plugins.Payments.Latipay.Fields.SubPaymentMethod.Required"));

        _validator.RuleFor(model => model.SelectedSubPaymentMethod)
            .Must(selectedMethod => string.IsNullOrWhiteSpace(selectedMethod)
                || enabledSelectionKeys.Contains(selectedMethod.Trim()))
            .When(_ => paymentMethodEnabled && currencySupported && enabledSelectionKeys.Count > 0)
            .WithMessage(GetResource(localizationService, "Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid"));
    }

    public ValidationResult Validate(PaymentInfoModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _validator.Validate(model);
    }

    private static string GetResource(ILocalizationService localizationService, string resourceKey)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        return localizationService.GetResourceAsync(resourceKey).GetAwaiter().GetResult();
    }
}
