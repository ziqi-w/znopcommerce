using Nop.Plugin.Payments.Latipay.Domain.Enums;

namespace Nop.Plugin.Payments.Latipay.Domain;

/// <summary>
/// Represents a resolved Latipay sub-payment method available to the customer.
/// </summary>
public sealed record LatipaySubPaymentMethodOption(
    string Key,
    string ProviderValue,
    string DisplayName,
    string LogoUrl,
    LatipayIntegrationMode IntegrationMode);
