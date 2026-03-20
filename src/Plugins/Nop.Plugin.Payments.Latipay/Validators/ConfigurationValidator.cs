using System.Linq.Expressions;
using FluentValidation;
using Nop.Plugin.Payments.Latipay.Models.Admin;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Latipay.Validators;

/// <summary>
/// Validates the admin configuration model.
/// </summary>
public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
{
    public ConfigurationValidator(ILocalizationService localizationService)
    {
        RuleFor(model => model.ApiBaseUrl)
            .Cascade(CascadeMode.Stop)
            .Must(HasValue)
            .When(RequiresLegacyConfiguration)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.ApiBaseUrl.Required"))
            .Must(BeValidHttpsUrl)
            .When(model => !string.IsNullOrWhiteSpace(model.ApiBaseUrl))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.ApiBaseUrl.Invalid"));

        RuleFor(model => model.ApiBaseUrl)
            .Must(value => !HasValue(value) || value.Trim().Length <= LatipayDefaults.MaxApiBaseUrlLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.ApiBaseUrl.Length"));

        RuleFor(model => model.CardApiBaseUrl)
            .Cascade(CascadeMode.Stop)
            .Must(HasValue)
            .When(RequiresCardConfiguration)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Required"))
            .Must(BeValidHttpsUrl)
            .When(model => !string.IsNullOrWhiteSpace(model.CardApiBaseUrl))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Invalid"));

        RuleFor(model => model.CardApiBaseUrl)
            .Must(value => !HasValue(value) || value.Trim().Length <= LatipayDefaults.MaxApiBaseUrlLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardApiBaseUrl.Length"));

        RuleFor(model => model.RequestTimeoutSeconds)
            .InclusiveBetween(LatipayDefaults.MinRequestTimeoutSeconds, LatipayDefaults.MaxRequestTimeoutSeconds)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.RequestTimeoutSeconds.Range"));

        RuleFor(model => model.ReconciliationPeriodMinutes)
            .InclusiveBetween(LatipayDefaults.MinReconciliationPeriodMinutes, LatipayDefaults.MaxReconciliationPeriodMinutes)
            .When(model => model.EnableReconciliationTask)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.ReconciliationPeriodMinutes.Range"));

        RuleFor(model => model.RetryGuardMinutes)
            .InclusiveBetween(LatipayDefaults.MinRetryGuardMinutes, LatipayDefaults.MaxRetryGuardMinutes)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.RetryGuardMinutes.Range"));

        RuleFor(model => model.UserId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(RequiresLegacyConfiguration)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.UserId.Required"));

        RuleFor(model => model.UserId)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxCredentialLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.UserId.Length"));

        RuleFor(model => model.WalletId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(RequiresLegacyConfiguration)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.WalletId.Required"));

        RuleFor(model => model.WalletId)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxCredentialLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.WalletId.Length"));

        RuleFor(model => model.ApiKey)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxCredentialLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.ApiKey.Length"));

        RuleFor(model => model)
            .Must(model => !RequiresLegacyConfiguration(model) || model.ApiKeyConfigured || !string.IsNullOrWhiteSpace(model.ApiKey))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.ApiKey.Required"));

        RuleFor(model => model.CardMerchantId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(RequiresCardConfiguration)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardMerchantId.Required"));

        RuleFor(model => model.CardMerchantId)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxCredentialLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardMerchantId.Length"));

        RuleFor(model => model.CardSiteId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .When(RequiresCardConfiguration)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardSiteId.Required"));

        RuleFor(model => model.CardSiteId)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxCardSiteIdLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardSiteId.Length"));

        RuleFor(model => model.CardSiteId)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().All(char.IsDigit))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardSiteId.Invalid"));

        RuleFor(model => model.CardPrivateKey)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxCredentialLength)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardPrivateKey.Length"));

        RuleFor(model => model)
            .Must(model => !RequiresCardConfiguration(model) || model.CardPrivateKeyConfigured || !string.IsNullOrWhiteSpace(model.CardPrivateKey))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.CardPrivateKey.Required"));

        RuleFor(model => model)
            .Must(HaveAtLeastOneEnabledSubPaymentMethod)
            .When(model => model.Enabled)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.SubPaymentMethod.AtLeastOne"));

        RuleFor(model => model)
            .Must(model => !model.EnablePartialRefunds || model.EnableRefunds)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.Latipay.Fields.EnablePartialRefunds.RequiresRefunds"));

        RuleForEachDisplayName(model => model.AlipayDisplayName, localizationService, "Plugins.Payments.Latipay.Fields.AlipayDisplayName");
        RuleForEachDisplayName(model => model.WechatPayDisplayName, localizationService, "Plugins.Payments.Latipay.Fields.WechatPayDisplayName");
        RuleForEachDisplayName(model => model.NzBanksDisplayName, localizationService, "Plugins.Payments.Latipay.Fields.NzBanksDisplayName");
        RuleForEachDisplayName(model => model.PayIdDisplayName, localizationService, "Plugins.Payments.Latipay.Fields.PayIdDisplayName");
        RuleForEachDisplayName(model => model.UpiUpopDisplayName, localizationService, "Plugins.Payments.Latipay.Fields.UpiUpopDisplayName");
        RuleForEachDisplayName(model => model.CardVmDisplayName, localizationService, "Plugins.Payments.Latipay.Fields.CardVmDisplayName");
    }

    private void RuleForEachDisplayName(Expression<Func<ConfigurationModel, string>> expression,
        ILocalizationService localizationService, string resourceKey)
    {
        RuleFor(expression)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= LatipayDefaults.MaxDisplayNameLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{resourceKey}.Length"));

        RuleFor(expression)
            .Must(value => string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(value.Trim()))
            .WithMessageAwait(localizationService.GetResourceAsync($"{resourceKey}.Invalid"));
    }

    private static bool BeValidHttpsUrl(string value)
    {
        if (!HasValue(value))
            return false;

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool HaveAtLeastOneEnabledSubPaymentMethod(ConfigurationModel model)
    {
        return model.EnableAlipay
            || model.EnableWechatPay
            || model.EnableNzBanks
            || model.EnablePayId
            || model.EnableUpiUpop
            || model.EnableCardVm;
    }

    private static bool RequiresLegacyConfiguration(ConfigurationModel model)
    {
        return model.Enabled && (model.EnableAlipay
            || model.EnableWechatPay
            || model.EnableNzBanks
            || model.EnablePayId
            || model.EnableUpiUpop);
    }

    private static bool RequiresCardConfiguration(ConfigurationModel model)
    {
        return model.Enabled && model.EnableCardVm;
    }
}
