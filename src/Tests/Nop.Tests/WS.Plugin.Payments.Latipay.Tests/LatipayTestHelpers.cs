using Moq;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using WS.Plugin.Payments.Latipay;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Services.Logging;
using Nop.Services.Localization;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests;

internal static class LatipayTestHelpers
{
    public static Mock<ILocalizationService> CreateLocalizationService()
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService
            .Setup(service => service.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => key);

        localizationService
            .Setup(service => service.GetResourceAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((string key, int _, bool _, string defaultValue, bool _) =>
                string.IsNullOrWhiteSpace(defaultValue) ? key : defaultValue);

        return localizationService;
    }

    public static Mock<ILogger> CreateLogger()
    {
        var logger = new Mock<ILogger>();
        logger.Setup(service => service.InformationAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<global::Nop.Core.Domain.Customers.Customer>()))
            .Returns(Task.CompletedTask);
        logger.Setup(service => service.WarningAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<global::Nop.Core.Domain.Customers.Customer>()))
            .Returns(Task.CompletedTask);
        logger.Setup(service => service.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<global::Nop.Core.Domain.Customers.Customer>()))
            .Returns(Task.CompletedTask);
        return logger;
    }

    public static Mock<ILatipayOrderNoteService> CreateOrderNoteService()
    {
        var orderNoteService = new Mock<ILatipayOrderNoteService>();
        orderNoteService.Setup(service => service.AddNoteAsync(It.IsAny<Order>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        orderNoteService.Setup(service => service.AddNoteIfAbsentAsync(It.IsAny<Order>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        return orderNoteService;
    }

    public static Mock<ILocker> CreateImmediateLocker()
    {
        var locker = new Mock<ILocker>();
        locker.Setup(service => service.PerformActionWithLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Func<Task>>()))
            .Returns(async (string _, TimeSpan _, Func<Task> action) =>
            {
                await action();
                return true;
            });

        locker.Setup(service => service.RunWithHeartbeatAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationTokenSource>()))
            .Returns((string _, TimeSpan _, TimeSpan _, Func<CancellationToken, Task> action, CancellationTokenSource cancellationTokenSource) =>
                action(cancellationTokenSource?.Token ?? CancellationToken.None));

        locker.Setup(service => service.CancelTaskAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        locker.Setup(service => service.IsTaskRunningAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        return locker;
    }

    public static LatipaySettings CreateSettings(Action<LatipaySettings> configure = null)
    {
        var settings = new LatipaySettings
        {
            Enabled = true,
            UserId = "user-123",
            WalletId = "wallet-456",
            ApiKey = "secret-key",
            ApiBaseUrl = LatipayDefaults.ApiBaseUrl,
            CardApiBaseUrl = LatipayDefaults.CardApiBaseUrl,
            CardMerchantId = "merchant-789",
            CardSiteId = "600013",
            CardPrivateKey = "card-private-key",
            RequestTimeoutSeconds = 15,
            DebugLogging = false,
            EnableRefunds = true,
            EnablePartialRefunds = true,
            EnableAlipay = true,
            EnableWechatPay = true,
            EnableNzBanks = true,
            EnablePayId = false,
            EnableUpiUpop = false,
            EnableCardVm = true,
            CardVmDisplayName = LatipayDefaults.DefaultCardVmDisplayName,
            RetryGuardMinutes = 10,
            ReconciliationPeriodMinutes = 5
        };

        configure?.Invoke(settings);
        return settings;
    }

    public static Order CreateOrder(Action<Order> configure = null)
    {
        var order = new Order
        {
            Id = 100,
            CustomOrderNumber = "100",
            OrderTotal = 100m,
            RefundedAmount = 0m,
            PaymentStatus = global::Nop.Core.Domain.Payments.PaymentStatus.Pending,
            PaymentMethodSystemName = LatipayDefaults.SystemName,
            CustomerCurrencyCode = LatipayDefaults.CurrencyCode,
            OrderStatus = OrderStatus.Pending,
            CustomerId = 10
        };

        configure?.Invoke(order);
        return order;
    }

    public static LatipayPaymentAttempt CreatePaymentAttempt(Action<LatipayPaymentAttempt> configure = null)
    {
        var attempt = new LatipayPaymentAttempt
        {
            Id = 500,
            OrderId = 100,
            AttemptNumber = 1,
            MerchantReference = "merchant-ref-1",
            SelectedSubPaymentMethod = LatipayDefaults.SubPaymentMethodKeys.Alipay,
            LatipayOrderId = "latipay-order-1",
            Amount = 100m,
            Currency = LatipayDefaults.CurrencyCode,
            RedirectCreatedOnUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-6),
            UpdatedOnUtc = DateTime.UtcNow.AddMinutes(-6)
        };

        configure?.Invoke(attempt);
        return attempt;
    }
}
