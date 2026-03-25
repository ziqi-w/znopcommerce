using FluentValidation;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using WS.Plugin.Misc.AliyunOssStorage.Models;

namespace WS.Plugin.Misc.AliyunOssStorage.Validators;

public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
{
    public ConfigurationValidator(AliyunOssStorageSettings settings,
        ILocalizationService localizationService)
    {
        static bool RequiresConnectionSettings(ConfigurationModel model) => model.Enabled || model.IsTestConnectionRequested;

        RuleFor(model => model.Endpoint)
            .NotEmpty()
            .When(RequiresConnectionSettings)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Endpoint.Required"));

        RuleFor(model => model.Endpoint)
            .MaximumLength(AliyunOssStorageDefaults.MaxEndpointLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Endpoint.Length"));

        RuleFor(model => model.BucketName)
            .NotEmpty()
            .When(RequiresConnectionSettings)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BucketName.Required"));

        RuleFor(model => model.BucketName)
            .MaximumLength(AliyunOssStorageDefaults.MaxBucketNameLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BucketName.Length"));

        RuleFor(model => model.Region)
            .NotEmpty()
            .When(RequiresConnectionSettings)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Region.Required"));

        RuleFor(model => model.Region)
            .MaximumLength(AliyunOssStorageDefaults.MaxRegionLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.Region.Length"));

        RuleFor(model => model.AccessKeyId)
            .NotEmpty()
            .When(RequiresConnectionSettings)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeyId.Required"));

        RuleFor(model => model.AccessKeyId)
            .MaximumLength(AliyunOssStorageDefaults.MaxAccessKeyIdLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeyId.Length"));

        RuleFor(model => model.AccessKeySecret)
            .Must((model, value) => !RequiresConnectionSettings(model)
                || !string.IsNullOrWhiteSpace(value)
                || !string.IsNullOrWhiteSpace(settings.AccessKeySecret))
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.AccessKeySecret.Required"));

        RuleFor(model => model.CustomBaseUrl)
            .Must(BeValidAbsoluteUrl)
            .When(model => !string.IsNullOrWhiteSpace(model.CustomBaseUrl))
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.CustomBaseUrl.Invalid"));

        RuleFor(model => model.CustomBaseUrl)
            .MaximumLength(AliyunOssStorageDefaults.MaxCustomBaseUrlLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.CustomBaseUrl.Length"));

        RuleFor(model => model.BaseThumbPathPrefix)
            .MaximumLength(AliyunOssStorageDefaults.MaxBaseThumbPathPrefixLength)
            .WithMessageAwait(localizationService.GetResourceAsync($"{AliyunOssStorageDefaults.LocaleResourcePrefix}.BaseThumbPathPrefix.Length"));
    }

    private static bool BeValidAbsoluteUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
