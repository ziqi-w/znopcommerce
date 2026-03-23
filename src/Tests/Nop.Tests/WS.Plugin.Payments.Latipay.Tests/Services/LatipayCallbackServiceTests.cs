using FluentAssertions;
using Moq;
using Nop.Core.Domain.Payments;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Services;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayCallbackServiceTests
{
    [Test]
    public async Task ProcessCallbackAsync_Should_Treat_Already_Processed_Callback_As_Duplicate()
    {
        var signatureService = new LatipaySignatureService();
        var settings = LatipayTestHelpers.CreateSettings();
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.CallbackVerified = true;
            paymentAttempt.PaymentCompletedOnUtc = DateTime.UtcNow.AddMinutes(-2);
        });
        var notification = new LatipayStatusNotification
        {
            MerchantReference = attempt.MerchantReference,
            PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.Alipay,
            Status = "paid",
            Currency = LatipayDefaults.CurrencyCode,
            Amount = "100.00",
            OrderId = attempt.LatipayOrderId
        };
        notification.Signature = signatureService.CreateStatusSignature(
            notification.MerchantReference,
            notification.PaymentMethod,
            notification.Status,
            notification.Currency,
            notification.Amount,
            settings.ApiKey);

        // Precompute the same idempotency key the service will compute.
        attempt.CallbackIdempotencyKey = "will-be-overridden";
        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByMerchantReferenceAsync(attempt.MerchantReference))
            .ReturnsAsync(attempt);

        var order = LatipayTestHelpers.CreateOrder(existingOrder =>
        {
            existingOrder.PaymentStatus = PaymentStatus.Paid;
            existingOrder.CaptureTransactionId = attempt.LatipayOrderId;
        });
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id))
            .ReturnsAsync(order);

        var callbackService = new LatipayCallbackService(
            LatipayTestHelpers.CreateLogger().Object,
            LatipayTestHelpers.CreateImmediateLocker().Object,
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            signatureService,
            Mock.Of<ILatipayStateMachine>(),
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            orderService.Object,
            settings);

        // First pass computes and stores the key through mocked state machine update path? Instead simulate persisted key.
        var duplicateKey = ComputeCallbackIdempotencyKey(notification);
        attempt.CallbackIdempotencyKey = duplicateKey;

        var result = await callbackService.ProcessCallbackAsync(notification);

        result.AcknowledgeCallback.Should().BeTrue();
        result.IsDuplicate.Should().BeTrue();
    }

    [Test]
    public async Task ProcessCallbackAsync_Should_Validate_HostedCard_Callback_With_PrivateKey()
    {
        var signatureService = new LatipaySignatureService();
        var settings = LatipayTestHelpers.CreateSettings();
        var attempt = LatipayTestHelpers.CreatePaymentAttempt(paymentAttempt =>
        {
            paymentAttempt.SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.CardVm;
        });

        var notification = new LatipayStatusNotification
        {
            MerchantReference = attempt.MerchantReference,
            NotifyVersion = "v2",
            OrderId = "provider-order-1",
            PaymentMethod = LatipayDefaults.ProviderSubPaymentMethodValues.CardVm,
            Status = "paid",
            Currency = LatipayDefaults.CurrencyCode,
            Amount = "100",
            PayTime = "2023-11-03 02:39:49"
        };
        notification.Signature = signatureService.CreateSortedParameterSignature(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = notification.Amount,
            ["currency"] = notification.Currency,
            ["notify_version"] = notification.NotifyVersion,
            ["order_id"] = notification.OrderId,
            ["out_trade_no"] = notification.MerchantReference,
            ["pay_time"] = notification.PayTime,
            ["payment_method"] = notification.PaymentMethod,
            ["status"] = notification.Status
        }, settings.CardPrivateKey, urlEncodeValues: true);

        var paymentAttemptService = new Mock<ILatipayPaymentAttemptService>();
        paymentAttemptService.Setup(service => service.GetByMerchantReferenceAsync(attempt.MerchantReference))
            .ReturnsAsync(attempt);

        var order = LatipayTestHelpers.CreateOrder();
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id))
            .ReturnsAsync(order);

        var stateMachine = new Mock<ILatipayStateMachine>();
        stateMachine.Setup(service => service.ApplyVerifiedStatusAsync(order, attempt, It.IsAny<LatipayStatusNotification>(), "callback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatipayStateTransitionResult
            {
                Changed = true,
                MarkedPaid = true,
                Message = "confirmed"
            });

        var callbackService = new LatipayCallbackService(
            LatipayTestHelpers.CreateLogger().Object,
            LatipayTestHelpers.CreateImmediateLocker().Object,
            LatipayTestHelpers.CreateOrderNoteService().Object,
            paymentAttemptService.Object,
            signatureService,
            stateMachine.Object,
            new LatipaySubPaymentMethodService(),
            new LatipayTransactionStatusMapper(),
            orderService.Object,
            settings);

        var result = await callbackService.ProcessCallbackAsync(notification);

        result.AcknowledgeCallback.Should().BeTrue();
        result.Message.Should().Be("confirmed");
    }

    private static string ComputeCallbackIdempotencyKey(LatipayStatusNotification notification)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var payload = string.Join("|", new[]
        {
            notification.MerchantReference,
            notification.PaymentMethod,
            notification.Status,
            notification.Currency,
            notification.Amount,
            notification.Signature
        });

        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
