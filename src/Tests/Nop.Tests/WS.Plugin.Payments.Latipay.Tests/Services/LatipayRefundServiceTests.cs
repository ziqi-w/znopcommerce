using FluentAssertions;
using Moq;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Api.Responses;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayRefundServiceTests
{
    [Test]
    public async Task EvaluateEligibilityAsync_Should_Deny_When_Remaining_Refundable_Amount_Is_Exceeded()
    {
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByOrderIdAsync(It.IsAny<int>()))
            .ReturnsAsync([
                LatipayTestHelpers.CreatePaymentAttempt(attempt =>
                {
                    attempt.PaymentCompletedOnUtc = DateTime.UtcNow.AddMinutes(-10);
                })
            ]);

        var refundRecordService = new Mock<ILatipayRefundRecordService>();
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.Succeeded)))
            .ReturnsAsync(30m);
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.PendingSubmission), nameof(LatipayRefundStatus.ReviewRequired)))
            .ReturnsAsync(20m);

        var service = CreateRefundService(
            paymentAttemptService: paymentAttemptService.Object,
            refundRecordService: refundRecordService.Object);

        var order = LatipayTestHelpers.CreateOrder(order =>
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.RefundedAmount = 25m;
        });

        var result = await service.EvaluateEligibilityAsync(order, 60m, isPartialRefund: true);

        result.CanRefund.Should().BeFalse();
        result.Message.Should().Contain("remaining refundable balance");
    }

    [Test]
    public async Task EvaluateEligibilityAsync_Should_Deny_When_Latipay_Order_Id_Is_Missing()
    {
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByOrderIdAsync(It.IsAny<int>()))
            .ReturnsAsync([
                LatipayTestHelpers.CreatePaymentAttempt(attempt =>
                {
                    attempt.PaymentCompletedOnUtc = DateTime.UtcNow.AddMinutes(-10);
                    attempt.LatipayOrderId = null;
                })
            ]);

        var service = CreateRefundService(paymentAttemptService: paymentAttemptService.Object);
        var order = LatipayTestHelpers.CreateOrder(order =>
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.CaptureTransactionId = "merchant-ref-1";
        });

        var result = await service.EvaluateEligibilityAsync(order, 10m, isPartialRefund: false);

        result.CanRefund.Should().BeFalse();
        result.Message.Should().Contain("identifier required for refund is missing");
    }

    [Test]
    public async Task RefundAsync_Should_Return_PartiallyRefunded_On_Successful_Partial_Refund()
    {
        var order = LatipayTestHelpers.CreateOrder(order =>
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.CaptureTransactionId = "latipay-order-1";
        });
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.PaymentCompletedOnUtc = DateTime.UtcNow.AddMinutes(-10);
        });

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByOrderIdAsync(order.Id))
            .ReturnsAsync([attempt]);

        var refundRecordService = new Mock<ILatipayRefundRecordService>();
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.Succeeded)))
            .ReturnsAsync(0m);
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.PendingSubmission), nameof(LatipayRefundStatus.ReviewRequired)))
            .ReturnsAsync(0m);
        LatipayRefundRecord insertedRecord = null;
        refundRecordService.Setup(service => service.InsertAsync(It.IsAny<LatipayRefundRecord>()))
            .Callback<LatipayRefundRecord>(record => insertedRecord = new LatipayRefundRecord
            {
                RefundStatus = record.RefundStatus,
                RefundReference = record.RefundReference,
                RefundAmount = record.RefundAmount
            })
            .Returns(Task.CompletedTask);
        refundRecordService.Setup(service => service.UpdateAsync(It.IsAny<LatipayRefundRecord>()))
            .Returns(Task.CompletedTask);
        refundRecordService.Setup(service => service.GetByRefundReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync((LatipayRefundRecord)null);

        var requestFactory = new Mock<ILatipayRequestFactory>();
        requestFactory.Setup(service => service.BuildRefundRequestAsync(It.IsAny<RefundRequestParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundRequest
            {
                UserId = "user",
                OrderId = "latipay-order-1",
                RefundAmount = "10.00",
                Reference = "refund-ref",
                Signature = "signature"
            });

        var apiClient = new Mock<ILatipayApiClient>();
        apiClient.Setup(service => service.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundResponse
            {
                Code = "0",
                Message = "ok"
            });

        var service = CreateRefundService(
            apiClient: apiClient.Object,
            paymentAttemptService: paymentAttemptService.Object,
            refundRecordService: refundRecordService.Object,
            requestFactory: requestFactory.Object,
            orderService: MockOrderService(order).Object);

        var result = await service.RefundAsync(new RefundPaymentRequest
        {
            Order = order,
            AmountToRefund = 10m,
            IsPartialRefund = true
        });

        result.Success.Should().BeTrue();
        result.NewPaymentStatus.Should().Be(PaymentStatus.PartiallyRefunded);
        insertedRecord.Should().NotBeNull();
        insertedRecord!.RefundStatus.Should().Be(nameof(LatipayRefundStatus.PendingSubmission));
        refundRecordService.Verify(service => service.UpdateAsync(It.Is<LatipayRefundRecord>(record =>
            record.RefundStatus == nameof(LatipayRefundStatus.Succeeded))), Times.Once);
    }

    [Test]
    public async Task RefundAsync_Should_Mark_Ambiguous_Failure_As_ReviewRequired_And_Return_Error()
    {
        var order = LatipayTestHelpers.CreateOrder(order =>
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.CaptureTransactionId = "latipay-order-1";
        });
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.PaymentCompletedOnUtc = DateTime.UtcNow.AddMinutes(-10);
        });

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByOrderIdAsync(order.Id))
            .ReturnsAsync([attempt]);

        var refundRecordService = new Mock<ILatipayRefundRecordService>();
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.Succeeded)))
            .ReturnsAsync(0m);
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.PendingSubmission), nameof(LatipayRefundStatus.ReviewRequired)))
            .ReturnsAsync(0m);
        refundRecordService.Setup(service => service.InsertAsync(It.IsAny<LatipayRefundRecord>()))
            .Returns(Task.CompletedTask);
        refundRecordService.Setup(service => service.UpdateAsync(It.IsAny<LatipayRefundRecord>()))
            .Returns(Task.CompletedTask);
        refundRecordService.Setup(service => service.GetByRefundReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync((LatipayRefundRecord)null);

        var requestFactory = new Mock<ILatipayRequestFactory>();
        requestFactory.Setup(service => service.BuildRefundRequestAsync(It.IsAny<RefundRequestParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundRequest
            {
                UserId = "user",
                OrderId = "latipay-order-1",
                RefundAmount = "10.00",
                Reference = "refund-ref",
                Signature = "signature"
            });

        var apiClient = new Mock<ILatipayApiClient>();
        apiClient.Setup(service => service.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LatipayApiException("timed out", LatipayApiFailureKind.Timeout, isTransient: true));

        var service = CreateRefundService(
            apiClient: apiClient.Object,
            paymentAttemptService: paymentAttemptService.Object,
            refundRecordService: refundRecordService.Object,
            requestFactory: requestFactory.Object,
            orderService: MockOrderService(order).Object);

        var result = await service.RefundAsync(new RefundPaymentRequest
        {
            Order = order,
            AmountToRefund = 10m,
            IsPartialRefund = false
        });

        result.Success.Should().BeFalse();
        result.Errors.Single().Should().Contain("manual review");
        refundRecordService.Verify(service => service.UpdateAsync(It.Is<LatipayRefundRecord>(record =>
            record.RefundStatus == nameof(LatipayRefundStatus.ReviewRequired))), Times.Once);
    }

    [Test]
    public async Task RefundAsync_Should_Use_HostedCard_Refund_Path_For_Card_Attempts()
    {
        var order = LatipayTestHelpers.CreateOrder(order =>
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.CaptureTransactionId = "provider-order-1";
        });
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.PaymentCompletedOnUtc = DateTime.UtcNow.AddMinutes(-10);
            paymentAttempt.SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.CardVm;
            paymentAttempt.LatipayOrderId = "provider-order-1";
        });

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByOrderIdAsync(order.Id))
            .ReturnsAsync([attempt]);
        paymentAttemptService.Setup(service => service.GetByIdAsync(attempt.Id))
            .ReturnsAsync(attempt);

        var refundRecordService = new Mock<ILatipayRefundRecordService>();
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.Succeeded)))
            .ReturnsAsync(0m);
        refundRecordService.Setup(service => service.GetTotalRefundAmountAsync(It.IsAny<int>(), nameof(LatipayRefundStatus.PendingSubmission), nameof(LatipayRefundStatus.ReviewRequired)))
            .ReturnsAsync(0m);
        refundRecordService.Setup(service => service.InsertAsync(It.IsAny<LatipayRefundRecord>()))
            .Returns(Task.CompletedTask);
        refundRecordService.Setup(service => service.UpdateAsync(It.IsAny<LatipayRefundRecord>()))
            .Returns(Task.CompletedTask);
        refundRecordService.Setup(service => service.GetByRefundReferenceAsync(It.IsAny<string>()))
            .ReturnsAsync((LatipayRefundRecord)null);

        var requestFactory = new Mock<ILatipayRequestFactory>();
        requestFactory.Setup(service => service.BuildCardRefundRequestAsync(It.IsAny<CardRefundRequestParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CardRefundRequest
            {
                MerchantId = "merchant-789",
                SiteId = 600013,
                OrderId = "provider-order-1",
                RefundAmount = "10",
                Reason = "refund-ref",
                Signature = "signature"
            });

        var apiClient = new Mock<ILatipayApiClient>();
        apiClient.Setup(service => service.RefundCardTransactionAsync(It.IsAny<CardRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CardRefundResponse
            {
                Code = "0",
                Message = "SUCCESS"
            });

        var service = CreateRefundService(
            apiClient: apiClient.Object,
            paymentAttemptService: paymentAttemptService.Object,
            refundRecordService: refundRecordService.Object,
            requestFactory: requestFactory.Object,
            orderService: MockOrderService(order).Object);

        var result = await service.RefundAsync(new RefundPaymentRequest
        {
            Order = order,
            AmountToRefund = 10m,
            IsPartialRefund = false
        });

        result.Success.Should().BeTrue();
        result.NewPaymentStatus.Should().Be(PaymentStatus.Refunded);
        requestFactory.Verify(service => service.BuildCardRefundRequestAsync(It.IsAny<CardRefundRequestParameters>(), It.IsAny<CancellationToken>()), Times.Once);
        apiClient.Verify(service => service.RefundCardTransactionAsync(It.IsAny<CardRefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static LatipayRefundService CreateRefundService(
        ILatipayApiClient apiClient = null,
        ILatipayPaymentAttemptService paymentAttemptService = null,
        ILatipayRefundRecordService refundRecordService = null,
        ILatipayRequestFactory requestFactory = null,
        IOrderService orderService = null)
    {
        return new LatipayRefundService(
            apiClient ?? Mock.Of<ILatipayApiClient>(),
            LatipayTestHelpers.CreateLogger().Object,
            LatipayTestHelpers.CreateImmediateLocker().Object,
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService ?? Mock.Of<ILatipayPaymentAttemptService>(),
            refundRecordService ?? Mock.Of<ILatipayRefundRecordService>(),
            requestFactory ?? Mock.Of<ILatipayRequestFactory>(),
            new LatipaySubPaymentMethodService(),
            orderService ?? MockOrderService(LatipayTestHelpers.CreateOrder()).Object,
            LatipayTestHelpers.CreateSettings());
    }

    private static Mock<IOrderService> MockOrderService(global::Nop.Core.Domain.Orders.Order order)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id))
            .ReturnsAsync(order);
        return orderService;
    }
}
