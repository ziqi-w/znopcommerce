using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Provides the configured Latipay sub-payment methods.
/// </summary>
public interface ILatipaySubPaymentMethodService
{
    IReadOnlyList<LatipaySubPaymentMethodOption> GetKnownMethods();

    IReadOnlyList<LatipaySubPaymentMethodOption> GetEnabledMethods(LatipaySettings settings);

    bool HasAnyEnabledMethods(LatipaySettings settings);

    bool TryGetMethod(string selectionKey, out LatipaySubPaymentMethodOption method);

    bool TryGetEnabledMethod(LatipaySettings settings, string selectionKey, out LatipaySubPaymentMethodOption method);

    bool TryGetMethodByProviderValue(string providerValue, out LatipaySubPaymentMethodOption method);

    bool HasEnabledMethods(LatipaySettings settings, LatipayIntegrationMode integrationMode);
}
