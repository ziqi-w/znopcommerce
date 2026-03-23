using FluentAssertions;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Models.Public;
using WS.Plugin.Payments.Latipay.Validators;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Validators;

[TestFixture]
public class PaymentInfoValidatorTests
{
    [Test]
    public void Should_Reject_When_No_SubPaymentMethod_Selected()
    {
        var localizationService = LatipayTestHelpers.CreateLocalizationService();
        var validator = new PaymentInfoValidator(
            localizationService.Object,
            [LatipayDefaults.SubPaymentMethodKeys.Alipay],
            paymentMethodEnabled: true,
            currencySupported: true);

        var result = validator.Validate(new PaymentInfoModel
        {
            SelectedSubPaymentMethod = string.Empty
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .Should().Contain("Plugins.Payments.Latipay.Fields.SubPaymentMethod.Required");
    }

    [Test]
    public void Should_Reject_When_Selected_SubPaymentMethod_Is_Not_Enabled()
    {
        var localizationService = LatipayTestHelpers.CreateLocalizationService();
        var validator = new PaymentInfoValidator(
            localizationService.Object,
            [LatipayDefaults.SubPaymentMethodKeys.Alipay],
            paymentMethodEnabled: true,
            currencySupported: true);

        var result = validator.Validate(new PaymentInfoModel
        {
            SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.WechatPay
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .Should().Contain("Plugins.Payments.Latipay.Fields.SubPaymentMethod.Invalid");
    }

    [Test]
    public void Should_Reject_When_Currency_Is_Not_Supported()
    {
        var localizationService = LatipayTestHelpers.CreateLocalizationService();
        var validator = new PaymentInfoValidator(
            localizationService.Object,
            [LatipayDefaults.SubPaymentMethodKeys.Alipay],
            paymentMethodEnabled: true,
            currencySupported: false);

        var result = validator.Validate(new PaymentInfoModel
        {
            SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.Alipay
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .Should().Contain("Plugins.Payments.Latipay.PaymentInfo.CurrencyNotSupported");
    }

    [Test]
    public void Should_Accept_Enabled_SubPaymentMethod()
    {
        var localizationService = LatipayTestHelpers.CreateLocalizationService();
        var validator = new PaymentInfoValidator(
            localizationService.Object,
            [LatipayDefaults.SubPaymentMethodKeys.Alipay, LatipayDefaults.SubPaymentMethodKeys.WechatPay],
            paymentMethodEnabled: true,
            currencySupported: true);

        var result = validator.Validate(new PaymentInfoModel
        {
            SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.WechatPay
        });

        result.IsValid.Should().BeTrue();
    }
}
