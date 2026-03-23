using FluentAssertions;
using Moq;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Services;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Orders;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayStateMachineTests
{
    [Test]
    public async Task ApplyVerifiedStatusAsync_Should_Mark_Order_Paid_When_Notification_Is_Valid()
    {
        var order = LatipayTestHelpers.CreateOrder(order => order.PaymentStatus = PaymentStatus.Pending);
        var attempt = LatipayTestHelpers.CreatePaymentAttempt();
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.UpdateAsync(It.IsAny<LatipayPaymentAttempt>()))
            .Returns(Task.CompletedTask);
        var orderProcessingService = new Mock<IOrderProcessingService>();
        orderProcessingService.Setup(service => service.CanMarkOrderAsPaid(order)).Returns(true);
        orderProcessingService.Setup(service => service.MarkOrderAsPaidAsync(order)).Returns(Task.CompletedTask);

        var stateMachine = new LatipayStateMachine(
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            orderProcessingService.Object);

        var result = await stateMachine.ApplyVerifiedStatusAsync(order, attempt, new LatipayStatusNotification
        {
            MerchantReference = attempt.MerchantReference,
            PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.Alipay,
            Status = "paid",
            Currency = LatipayDefaults.CurrencyCode,
            Amount = "100.00",
            OrderId = "latipay-order-1"
        }, "callback");

        result.MarkedPaid.Should().BeTrue();
        order.CaptureTransactionId.Should().Be("latipay-order-1");
        paymentAttemptService.Verify(service => service.UpdateAsync(It.Is<LatipayPaymentAttempt>(updated => updated.PaymentCompletedOnUtc.HasValue)), Times.Once);
        orderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(order), Times.Once);
    }

    [Test]
    public async Task ApplyVerifiedStatusAsync_Should_Require_Review_When_Amount_Does_Not_Match()
    {
        var order = LatipayTestHelpers.CreateOrder();
        var attempt = LatipayTestHelpers.CreatePaymentAttempt();
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.UpdateAsync(It.IsAny<LatipayPaymentAttempt>()))
            .Returns(Task.CompletedTask);

        var stateMachine = new LatipayStateMachine(
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            Mock.Of<IOrderProcessingService>());

        var result = await stateMachine.ApplyVerifiedStatusAsync(order, attempt, new LatipayStatusNotification
        {
            MerchantReference = attempt.MerchantReference,
            PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.Alipay,
            Status = "paid",
            Currency = LatipayDefaults.CurrencyCode,
            Amount = "50.00"
        }, "callback");

        result.ReviewRequired.Should().BeTrue();
        result.KeepPending.Should().BeTrue();
        result.Message.Should().Contain("original attempt amount");
    }

    [Test]
    public async Task ApplyVerifiedStatusAsync_Should_Require_Review_For_Illegal_Transition()
    {
        var order = LatipayTestHelpers.CreateOrder();
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.ExternalStatus = "failed";
        });
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.UpdateAsync(It.IsAny<LatipayPaymentAttempt>()))
            .Returns(Task.CompletedTask);

        var stateMachine = new LatipayStateMachine(
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            Mock.Of<IOrderProcessingService>());

        var result = await stateMachine.ApplyVerifiedStatusAsync(order, attempt, new LatipayStatusNotification
        {
            MerchantReference = attempt.MerchantReference,
            PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.Alipay,
            Status = "paid",
            Currency = LatipayDefaults.CurrencyCode,
            Amount = "100.00"
        }, "callback");

        result.ReviewRequired.Should().BeTrue();
        result.KeepPending.Should().BeTrue();
        result.Message.Should().Contain("Manual review is required");
    }
}
