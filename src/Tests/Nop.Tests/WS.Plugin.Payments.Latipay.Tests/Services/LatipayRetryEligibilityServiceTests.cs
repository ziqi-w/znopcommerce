using FluentAssertions;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Services;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayRetryEligibilityServiceTests
{
    [Test]
    public async Task EvaluateAsync_Should_Allow_When_No_Attempts_Exist()
    {
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetLatestByOrderIdAsync(It.IsAny<int>()))
            .ReturnsAsync((LatipayPaymentAttempt)null);

        var reconciliationService = new Mock<ILatipayReconciliationService>();
        var service = new LatipayRetryEligibilityService(
            paymentAttemptService.Object,
            reconciliationService.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            LatipayTestHelpers.CreateSettings());

        var result = await service.EvaluateAsync(LatipayTestHelpers.CreateOrder());

        result.CanRetry.Should().BeTrue();
        result.Message.Should().Contain("Choose a Latipay payment option");
        reconciliationService.Verify(recon => recon.CanRetryPaymentAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EvaluateAsync_Should_Deny_When_Order_Is_Already_Paid()
    {
        var service = new LatipayRetryEligibilityService(
            Mock.Of<ILatipayPaymentAttemptService>(),
            Mock.Of<ILatipayReconciliationService>(),
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            LatipayTestHelpers.CreateSettings());

        var result = await service.EvaluateAsync(LatipayTestHelpers.CreateOrder(order =>
        {
            order.PaymentStatus = PaymentStatus.Paid;
        }));

        result.CanRetry.Should().BeFalse();
    }

    [Test]
    public async Task EvaluateAsync_Should_Deny_When_Reconciliation_Says_Retry_Is_Not_Safe()
    {
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetLatestByOrderIdAsync(It.IsAny<int>()))
            .ReturnsAsync(LatipayTestHelpers.CreatePaymentAttempt(attempt =>
            {
                attempt.ExternalStatus = "pending";
            }));

        var reconciliationService = new Mock<ILatipayReconciliationService>();
        reconciliationService.Setup(service => service.CanRetryPaymentAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new LatipayRetryEligibilityService(
            paymentAttemptService.Object,
            reconciliationService.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            LatipayTestHelpers.CreateSettings());

        var result = await service.EvaluateAsync(LatipayTestHelpers.CreateOrder());

        result.CanRetry.Should().BeFalse();
        result.Message.Should().Contain("still confirming");
    }

    [Test]
    public async Task EvaluateAsync_Should_Allow_When_Previous_Attempt_Did_Not_Start()
    {
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetLatestByOrderIdAsync(It.IsAny<int>()))
            .ReturnsAsync(LatipayTestHelpers.CreatePaymentAttempt(attempt =>
            {
                attempt.RedirectCreatedOnUtc = null;
            }));

        var service = new LatipayRetryEligibilityService(
            paymentAttemptService.Object,
            Mock.Of<ILatipayReconciliationService>(),
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            LatipayTestHelpers.CreateSettings());

        var result = await service.EvaluateAsync(LatipayTestHelpers.CreateOrder());

        result.CanRetry.Should().BeTrue();
        result.Message.Should().Contain("did not start successfully");
    }
}
