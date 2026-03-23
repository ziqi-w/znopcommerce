using FluentAssertions;
using WS.Plugin.Payments.Latipay.Services;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipaySignatureServiceTests
{
    private LatipaySignatureService _signatureService;

    [SetUp]
    public void SetUp()
    {
        _signatureService = new LatipaySignatureService();
    }

    [Test]
    public void CreateRequestSignature_ShouldIgnoreOrderAndEmptyValues()
    {
        var valuesA = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["wallet_id"] = "wallet",
            ["user_id"] = "user",
            ["payment_method"] = "alipay",
            ["empty_field"] = ""
        };
        var valuesB = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["payment_method"] = "alipay",
            ["user_id"] = "user",
            ["wallet_id"] = "wallet"
        };

        var signatureA = _signatureService.CreateRequestSignature(valuesA, "secret");
        var signatureB = _signatureService.CreateRequestSignature(valuesB, " secret ");

        signatureA.Should().Be(signatureB);
    }

    [Test]
    public void HostedResponseSignature_ShouldRoundTrip()
    {
        var signature = _signatureService.CreateHostedResponseSignature("nonce-1", "https://host.example/pay", "secret");

        _signatureService.IsHostedResponseSignatureValid("nonce-1", "https://host.example/pay", signature, "secret")
            .Should().BeTrue();
        _signatureService.IsHostedResponseSignatureValid("nonce-2", "https://host.example/pay", signature, "secret")
            .Should().BeFalse();
    }

    [Test]
    public void StatusSignature_ShouldRoundTrip()
    {
        var signature = _signatureService.CreateStatusSignature("merchant-ref", "alipay", "paid", "NZD", "10.00", "secret");

        _signatureService.IsStatusSignatureValid("merchant-ref", "alipay", "paid", "NZD", "10.00", signature, "secret")
            .Should().BeTrue();
        _signatureService.IsStatusSignatureValid("merchant-ref", "alipay", "paid", "NZD", "10.01", signature, "secret")
            .Should().BeFalse();
    }

    [Test]
    public void CreateSortedParameterSignature_Should_Support_Documented_Card_Callback_Encoding()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "0.10",
            ["currency"] = "NZD",
            ["notify_version"] = "v2",
            ["order_id"] = "231103-0237511-001",
            ["out_trade_no"] = "231103-0237511",
            ["pay_time"] = "2023-11-03 02:39:49",
            ["payment_method"] = "vm",
            ["status"] = "paid"
        };

        var signature = _signatureService.CreateSortedParameterSignature(values, "card-secret", urlEncodeValues: true);

        _signatureService.IsSortedParameterSignatureValid(values, signature, "card-secret", urlEncodeValues: true)
            .Should().BeTrue();
        _signatureService.IsSortedParameterSignatureValid(values, signature, "wrong-secret", urlEncodeValues: true)
            .Should().BeFalse();
    }

    [Test]
    public void AreEqual_ShouldUseNormalizedFixedTimeComparison()
    {
        var expected = _signatureService.CreateHostedResponseSignature("nonce", "https://host", "secret");

        _signatureService.AreEqual(expected, expected.ToUpperInvariant()).Should().BeFalse();
        _signatureService.AreEqual(expected, expected).Should().BeTrue();
        _signatureService.AreEqual(expected, null).Should().BeFalse();
    }
}
