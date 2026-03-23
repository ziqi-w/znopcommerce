using System.Text.RegularExpressions;
using FluentValidation;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using WS.Plugin.Misc.GoogleMerchantCenter;
using WS.Plugin.Misc.GoogleMerchantCenter.Models.Admin;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Validators;

public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
{
    private static readonly Regex CurrencyCodeRegex = new("^[A-Za-z]{3}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CountryCodeRegex = new("^[A-Za-z]{2}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public ConfigurationValidator(ILocalizationService localizationService)
    {
        RuleFor(model => model.FeedToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedToken.Required"))
            .MaximumLength(GoogleMerchantCenterDefaults.MaxFeedTokenLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedToken.Length"));

        RuleFor(model => model.DefaultCurrencyCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCurrencyCode.Required"))
            .Must(value => !string.IsNullOrWhiteSpace(value) && CurrencyCodeRegex.IsMatch(value.Trim()))
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCurrencyCode.Invalid"));

        RuleFor(model => model.DefaultCountryCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCountryCode.Required"))
            .Must(value => !string.IsNullOrWhiteSpace(value) && CountryCodeRegex.IsMatch(value.Trim()))
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultCountryCode.Invalid"));

        RuleFor(model => model.FeedRegenerationIntervalMinutes)
            .InclusiveBetween(
                GoogleMerchantCenterDefaults.MinFeedRegenerationIntervalMinutes,
                GoogleMerchantCenterDefaults.MaxFeedRegenerationIntervalMinutes)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.FeedRegenerationIntervalMinutes.Range"));

        RuleFor(model => model.DefaultShippingCountryCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(model => model.IncludeShipping)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingCountryCode.Required"))
            .Must(value => !string.IsNullOrWhiteSpace(value) && CountryCodeRegex.IsMatch(value.Trim()))
            .When(model => model.IncludeShipping)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingCountryCode.Invalid"));

        RuleFor(model => model.DefaultShippingService)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(model => model.IncludeShipping)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingService.Required"))
            .MaximumLength(GoogleMerchantCenterDefaults.MaxShippingServiceLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingService.Length"));

        RuleFor(model => model.DefaultShippingPrice)
            .GreaterThanOrEqualTo(0m)
            .When(model => model.IncludeShipping)
            .WithMessageAwait(localizationService.GetResourceAsync($"{GoogleMerchantCenterDefaults.LocaleResourcePrefix}.Fields.DefaultShippingPrice.Range"));
    }
}
