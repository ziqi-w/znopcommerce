using Nop.Plugin.Payments.Latipay.Domain;
using Nop.Plugin.Payments.Latipay.Domain.Enums;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;

namespace Nop.Plugin.Payments.Latipay.Services;

/// <summary>
/// Resolves configured Latipay sub-payment methods from plugin settings.
/// </summary>
public class LatipaySubPaymentMethodService : ILatipaySubPaymentMethodService
{
    private sealed record MethodDefinition(string Key,
        string ProviderValue,
        string DefaultDisplayName,
        string LogoUrl,
        LatipayIntegrationMode IntegrationMode,
        IReadOnlyCollection<string> ProviderAliases,
        Func<LatipaySettings, bool> EnabledSelector,
        Func<LatipaySettings, string> DisplayNameSelector);

    private static readonly IReadOnlyList<MethodDefinition> MethodDefinitions =
    [
        new(
            LatipayDefaults.SubPaymentMethodKeys.Alipay,
            LatipayDefaults.ProviderSubPaymentMethodValues.Alipay,
            LatipayDefaults.DefaultAlipayDisplayName,
            $"{LatipayDefaults.Content.PaymentOptionLogoFolderPath}/alipay.png",
            LatipayIntegrationMode.HostedOnline,
            [],
            settings => settings.EnableAlipay,
            settings => settings.AlipayDisplayName),
        new(
            LatipayDefaults.SubPaymentMethodKeys.WechatPay,
            LatipayDefaults.ProviderSubPaymentMethodValues.WechatPay,
            LatipayDefaults.DefaultWechatPayDisplayName,
            $"{LatipayDefaults.Content.PaymentOptionLogoFolderPath}/wechat-pay.png",
            LatipayIntegrationMode.HostedOnline,
            [],
            settings => settings.EnableWechatPay,
            settings => settings.WechatPayDisplayName),
        new(
            LatipayDefaults.SubPaymentMethodKeys.NzBanks,
            LatipayDefaults.ProviderSubPaymentMethodValues.NzBanks,
            LatipayDefaults.DefaultNzBanksDisplayName,
            $"{LatipayDefaults.Content.PaymentOptionLogoFolderPath}/nz-bank-transfer.png",
            LatipayIntegrationMode.HostedOnline,
            [LatipayDefaults.ProviderSubPaymentMethodValues.NzBanksReturnAlias],
            settings => settings.EnableNzBanks,
            settings => settings.NzBanksDisplayName),
        new(
            LatipayDefaults.SubPaymentMethodKeys.PayId,
            LatipayDefaults.ProviderSubPaymentMethodValues.PayId,
            LatipayDefaults.DefaultPayIdDisplayName,
            $"{LatipayDefaults.Content.PaymentOptionLogoFolderPath}/payid.png",
            LatipayIntegrationMode.HostedOnline,
            [],
            settings => settings.EnablePayId,
            settings => settings.PayIdDisplayName),
        new(
            LatipayDefaults.SubPaymentMethodKeys.UpiUpop,
            LatipayDefaults.ProviderSubPaymentMethodValues.UpiUpop,
            LatipayDefaults.DefaultUpiUpopDisplayName,
            $"{LatipayDefaults.Content.PaymentOptionLogoFolderPath}/unionpay.png",
            LatipayIntegrationMode.HostedOnline,
            [],
            settings => settings.EnableUpiUpop,
            settings => settings.UpiUpopDisplayName),
        new(
            LatipayDefaults.SubPaymentMethodKeys.CardVm,
            LatipayDefaults.ProviderSubPaymentMethodValues.CardVm,
            LatipayDefaults.DefaultCardVmDisplayName,
            $"{LatipayDefaults.Content.PaymentOptionLogoFolderPath}/card-visa-mastercard.png",
            LatipayIntegrationMode.HostedCard,
            [],
            settings => settings.EnableCardVm,
            settings => settings.CardVmDisplayName)
    ];

    public IReadOnlyList<LatipaySubPaymentMethodOption> GetKnownMethods()
    {
        return MethodDefinitions
            .Select(definition => new LatipaySubPaymentMethodOption(
                definition.Key,
                definition.ProviderValue,
                definition.DefaultDisplayName,
                definition.LogoUrl,
                definition.IntegrationMode))
            .ToList();
    }

    public IReadOnlyList<LatipaySubPaymentMethodOption> GetEnabledMethods(LatipaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return MethodDefinitions
            .Where(definition => definition.EnabledSelector(settings))
            .Select(definition => new LatipaySubPaymentMethodOption(
                definition.Key,
                definition.ProviderValue,
                GetDisplayName(definition, settings),
                definition.LogoUrl,
                definition.IntegrationMode))
            .ToList();
    }

    public bool HasAnyEnabledMethods(LatipaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return MethodDefinitions.Any(definition => definition.EnabledSelector(settings));
    }

    public bool HasEnabledMethods(LatipaySettings settings, LatipayIntegrationMode integrationMode)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return MethodDefinitions.Any(definition =>
            definition.IntegrationMode == integrationMode
            && definition.EnabledSelector(settings));
    }

    public bool TryGetMethod(string selectionKey, out LatipaySubPaymentMethodOption method)
    {
        var normalizedSelectionKey = NormalizeSelectionKey(selectionKey);
        method = GetKnownMethods()
            .FirstOrDefault(item => item.Key.Equals(normalizedSelectionKey, StringComparison.OrdinalIgnoreCase));

        return method is not null;
    }

    public bool TryGetEnabledMethod(LatipaySettings settings, string selectionKey, out LatipaySubPaymentMethodOption method)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedSelectionKey = NormalizeSelectionKey(selectionKey);
        method = GetEnabledMethods(settings)
            .FirstOrDefault(item => item.Key.Equals(normalizedSelectionKey, StringComparison.OrdinalIgnoreCase));

        return method is not null;
    }

    public bool TryGetMethodByProviderValue(string providerValue, out LatipaySubPaymentMethodOption method)
    {
        var normalizedProviderValue = NormalizeProviderValue(providerValue);
        var definition = MethodDefinitions.FirstOrDefault(item =>
            item.ProviderValue.Equals(normalizedProviderValue, StringComparison.OrdinalIgnoreCase)
            || item.ProviderAliases.Any(alias => alias.Equals(normalizedProviderValue, StringComparison.OrdinalIgnoreCase)));

        method = definition is null
            ? null
            : new LatipaySubPaymentMethodOption(
                definition.Key,
                definition.ProviderValue,
                definition.DefaultDisplayName,
                definition.LogoUrl,
                definition.IntegrationMode);

        return method is not null;
    }

    private static string GetDisplayName(MethodDefinition definition, LatipaySettings settings)
    {
        var configuredValue = definition.DisplayNameSelector(settings);
        return string.IsNullOrWhiteSpace(configuredValue)
            ? definition.DefaultDisplayName
            : configuredValue.Trim();
    }

    private static string NormalizeSelectionKey(string selectionKey)
    {
        return string.IsNullOrWhiteSpace(selectionKey)
            ? string.Empty
            : selectionKey.Trim();
    }

    private static string NormalizeProviderValue(string providerValue)
    {
        return string.IsNullOrWhiteSpace(providerValue)
            ? string.Empty
            : providerValue.Trim();
    }
}
