using FluentAssertions;
using Moq;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Services;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Api.Responses;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayReconciliationServiceTests
{
    [Test]
    public async Task ReconcileByMerchantReferenceAsync_Should_Apply_Verified_Query_Status()
    {
        var order = LatipayTestHelpers.CreateOrder();
        var attempt = LatipayTestHelpers.CreatePaymentAttempt();

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByMerchantReferenceAsync(attempt.MerchantReference))
            .ReturnsAsync(attempt);

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id))
            .ReturnsAsync(order);

        var requestFactory = new Mock<ILatipayRequestFactory>();
        requestFactory.Setup(service => service.BuildQueryTransactionRequestAsync(It.IsAny<QueryTransactionRequestParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryTransactionRequest
            {
                MerchantReference = attempt.MerchantReference,
                UserId = "user",
                Signature = "signature"
            });

        var apiClient = new Mock<ILatipayApiClient>();
        apiClient.Setup(service => service.QueryTransactionAsync(It.IsAny<QueryTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryTransactionResponse
            {
                MerchantReference = attempt.MerchantReference,
                PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.Alipay,
                Status = "paid",
                Currency = LatipayDefaults.CurrencyCode,
                Amount = "100.00",
                OrderId = "latipay-order-1",
                Signature = "signature"
            });

        var stateMachine = new Mock<ILatipayStateMachine>();
        stateMachine.Setup(service => service.ApplyVerifiedStatusAsync(order, attempt, It.IsAny<LatipayStatusNotification>(), "manual recheck", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatipayStateTransitionResult
            {
                Changed = true,
                MarkedPaid = true,
                Message = "confirmed"
            });

        var service = new LatipayReconciliationService(
            apiClient.Object,
            LatipayTestHelpers.CreateLogger().Object,
            LatipayTestHelpers.CreateImmediateLocker().Object,
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            requestFactory.Object,
            stateMachine.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            orderService.Object,
            LatipayTestHelpers.CreateSettings());

        var result = await service.ReconcileByMerchantReferenceAsync(attempt.MerchantReference, "manual recheck");

        result.IsVerified.Should().BeTrue();
        result.IsPaid.Should().BeTrue();
        result.Message.Should().Be("confirmed");
    }

    [Test]
    public async Task ReconcileByMerchantReferenceAsync_Should_Keep_Order_Pending_On_Unverifiable_Result()
    {
        var order = LatipayTestHelpers.CreateOrder(existingOrder => existingOrder.PaymentStatus = PaymentStatus.Pending);
        var attempt = LatipayTestHelpers.CreatePaymentAttempt();

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByMerchantReferenceAsync(attempt.MerchantReference))
            .ReturnsAsync(attempt);
        paymentAttemptService.Setup(service => service.UpdateAsync(It.IsAny<LatipayPaymentAttempt>()))
            .Returns(Task.CompletedTask);

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id))
            .ReturnsAsync(order);

        var requestFactory = new Mock<ILatipayRequestFactory>();
        requestFactory.Setup(service => service.BuildQueryTransactionRequestAsync(It.IsAny<QueryTransactionRequestParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryTransactionRequest
            {
                MerchantReference = attempt.MerchantReference,
                UserId = "user",
                Signature = "signature"
            });

        var apiClient = new Mock<ILatipayApiClient>();
        apiClient.Setup(service => service.QueryTransactionAsync(It.IsAny<QueryTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LatipayApiException("invalid signature", LatipayApiFailureKind.SignatureValidation));

        var service = new LatipayReconciliationService(
            apiClient.Object,
            LatipayTestHelpers.CreateLogger().Object,
            LatipayTestHelpers.CreateImmediateLocker().Object,
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            requestFactory.Object,
            Mock.Of<ILatipayStateMachine>(),
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            orderService.Object,
            LatipayTestHelpers.CreateSettings());

        var result = await service.ReconcileByMerchantReferenceAsync(attempt.MerchantReference, "manual recheck");

        result.KeepPending.Should().BeTrue();
        result.ReviewRequired.Should().BeTrue();
        result.IsPaid.Should().BeFalse();
    }

    [Test]
    public async Task ReconcileByMerchantReferenceAsync_Should_Use_HostedCard_Query_For_Card_Attempts()
    {
        var order = LatipayTestHelpers.CreateOrder();
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.CardVm;
        });

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByMerchantReferenceAsync(attempt.MerchantReference))
            .ReturnsAsync(attempt);

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id))
            .ReturnsAsync(order);

        var requestFactory = new Mock<ILatipayRequestFactory>();
        requestFactory.Setup(service => service.BuildCardQueryTransactionRequestAsync(It.IsAny<CardQueryTransactionRequestParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CardQueryTransactionRequest
            {
                MerchantId = "merchant-789",
                SiteId = 600013,
                OrderId = attempt.MerchantReference,
                Timestamp = 1234567890,
                Signature = "signature"
            });

        var apiClient = new Mock<ILatipayApiClient>();
        apiClient.Setup(service => service.QueryCardTransactionAsync(It.IsAny<CardQueryTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CardQueryTransactionResponse
            {
                Code = "0",
                Message = "SUCCESS",
                OrderId = attempt.MerchantReference,
                MerchantId = "merchant-789",
                SiteId = "600013",
                Currency = LatipayDefaults.CurrencyCode,
                Amount = "100",
                Status = "paid",
                RefundFlag = "0",
                Signature = "signature"
            });

        var stateMachine = new Mock<ILatipayStateMachine>();
        stateMachine.Setup(service => service.ApplyVerifiedStatusAsync(order, attempt, It.IsAny<LatipayStatusNotification>(), "manual recheck", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatipayStateTransitionResult
            {
                Changed = true,
                MarkedPaid = true,
                Message = "confirmed"
            });

        var service = new LatipayReconciliationService(
            apiClient.Object,
            LatipayTestHelpers.CreateLogger().Object,
            LatipayTestHelpers.CreateImmediateLocker().Object,
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            requestFactory.Object,
            stateMachine.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            orderService.Object,
            LatipayTestHelpers.CreateSettings());

        var result = await service.ReconcileByMerchantReferenceAsync(attempt.MerchantReference, "manual recheck");

        result.IsVerified.Should().BeTrue();
        result.IsPaid.Should().BeTrue();
        requestFactory.Verify(service => service.BuildCardQueryTransactionRequestAsync(It.IsAny<CardQueryTransactionRequestParameters>(), It.IsAny<CancellationToken>()), Times.Once);
        apiClient.Verify(service => service.QueryCardTransactionAsync(It.IsAny<CardQueryTransactionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
