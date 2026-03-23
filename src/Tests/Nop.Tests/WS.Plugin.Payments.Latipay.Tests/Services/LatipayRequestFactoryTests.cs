using FluentAssertions;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Services;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayRequestFactoryTests
{
    private LatipayRequestFactory _requestFactory;

    [SetUp]
    public void SetUp()
    {
        _requestFactory = new LatipayRequestFactory(
            new LatipaySignatureService(),
            new LatipaySubPaymentMethodService(),
            LatipayTestHelpers.CreateSettings());
    }

    [Test]
    public async Task BuildCreateTransactionRequestAsync_Should_Reject_NonNzd_Currency()
    {
        var action = async () => await _requestFactory.BuildCreateTransactionRequestAsync(new CreateTransactionRequestParameters
        {
            SubPaymentMethodKey = LatipayDefaults.SubPaymentMethodKeys.Alipay,
            Amount = 10m,
            CurrencyCode = "USD",
            ReturnUrl = "https://store.example/return",
            CallbackUrl = "https://store.example/callback",
            MerchantReference = "merchant-ref",
            CustomerIpAddress = "127.0.0.1",
            ProductName = "Test product"
        });

        var exception = await action.Should().ThrowAsync<LatipayApiException>();
        exception.Which.FailureKind.Should().Be(LatipayApiFailureKind.RequestValidation);
    }

    [Test]
    public async Task BuildCreateTransactionRequestAsync_Should_Reject_NonPositive_Amount()
    {
        var action = async () => await _requestFactory.BuildCreateTransactionRequestAsync(new CreateTransactionRequestParameters
        {
            SubPaymentMethodKey = LatipayDefaults.SubPaymentMethodKeys.Alipay,
            Amount = 0m,
            CurrencyCode = LatipayDefaults.CurrencyCode,
            ReturnUrl = "https://store.example/return",
            CallbackUrl = "https://store.example/callback",
            MerchantReference = "merchant-ref",
            CustomerIpAddress = "127.0.0.1",
            ProductName = "Test product"
        });

        var exception = await action.Should().ThrowAsync<LatipayApiException>();
        exception.Which.FailureKind.Should().Be(LatipayApiFailureKind.RequestValidation);
    }

    [Test]
    public async Task BuildCreateTransactionRequestAsync_Should_Build_Signed_Request_For_Nzd()
    {
        var request = await _requestFactory.BuildCreateTransactionRequestAsync(new CreateTransactionRequestParameters
        {
            SubPaymentMethodKey = LatipayDefaults.SubPaymentMethodKeys.Alipay,
            Amount = 10.12m,
            CurrencyCode = LatipayDefaults.CurrencyCode,
            ReturnUrl = "https://store.example/return",
            CallbackUrl = "https://store.example/callback",
            BackPageUrl = "https://store.example/back",
            MerchantReference = "merchant-ref",
            CustomerIpAddress = "127.0.0.1",
            ProductName = "Test product"
        });

        request.Amount.Should().Be("10.12");
        request.PaymentMethod.Should().Be(LatipayDefaults.ProviderSubPaymentMethodValues.Alipay);
        request.Signature.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task BuildCardCreateTransactionRequestAsync_Should_Build_Signed_Request_For_Nzd()
    {
        var request = await _requestFactory.BuildCardCreateTransactionRequestAsync(new CardCreateTransactionRequestParameters
        {
            Amount = 10.10m,
            CurrencyCode = LatipayDefaults.CurrencyCode,
            MerchantReference = "merchant-ref",
            ProductName = "Card product",
            ReturnUrl = "https://store.example/return?merchant_reference=merchant-ref",
            CallbackUrl = "https://store.example/callback",
            CancelOrderUrl = "https://store.example/retry",
            Payer = new CardPayerDetails
            {
                FirstName = "Jane",
                LastName = "Doe",
                Address = "1 Queen Street",
                CountryCode = "NZ",
                State = "Auckland",
                City = "Auckland",
                Postcode = "1010",
                Email = "jane@example.com",
                Phone = "+64270000000"
            }
        });

        request.Amount.Should().Be("10.1");
        request.OrderId.Should().Be("merchant-ref");
        request.MerchantId.Should().Be("merchant-789");
        request.SiteId.Should().Be(600013);
        request.Signature.Should().NotBeNullOrWhiteSpace();
    }
}
