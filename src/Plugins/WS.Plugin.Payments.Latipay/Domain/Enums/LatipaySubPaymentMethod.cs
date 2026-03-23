namespace WS.Plugin.Payments.Latipay.Domain.Enums;

/// <summary>
/// Represents documented Latipay hosted sub-payment methods used by this plugin.
/// </summary>
public enum LatipaySubPaymentMethod
{
    Alipay = 0,
    Wechat = 10,
    NzBanks = 20,
    PayId = 30,
    UpiUpop = 40,
    CardVm = 50
}
