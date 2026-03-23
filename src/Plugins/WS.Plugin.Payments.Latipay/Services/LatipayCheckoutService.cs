using System.Net;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Http;
using Nop.Core.Domain.Orders;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Interfaces;
using WS.Plugin.Payments.Latipay.Services.Models;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Framework.Mvc.Routing;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Starts hosted Latipay checkout attempts for an existing nopCommerce order.
/// </summary>
public class LatipayCheckoutService : ILatipayCheckoutService
{
    private const int MaxProductNameLength = 120;
    private static readonly TimeSpan RetryLockExpiration = TimeSpan.FromMinutes(2);

    private readonly ILatipayApiClient _latipayApiClient;
    private readonly ILogger _logger;
    private readonly ILocker _locker;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly ILatipayOrderNoteService _latipayOrderNoteService;
    private readonly ILatipayPaymentAttemptService _latipayPaymentAttemptService;
    private readonly ILatipayRequestFactory _latipayRequestFactory;
    private readonly ILatipayRetryEligibilityService _latipayRetryEligibilityService;
    private readonly ILatipaySubPaymentMethodService _latipaySubPaymentMethodService;
    private readonly IOrderService _orderService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IWebHelper _webHelper;
    private readonly LatipaySettings _settings;

    public LatipayCheckoutService(ILatipayApiClient latipayApiClient,
        ILogger logger,
        ILocker locker,
        IAddressService addressService,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        ILatipayOrderNoteService latipayOrderNoteService,
        ILatipayPaymentAttemptService latipayPaymentAttemptService,
        ILatipayRequestFactory latipayRequestFactory,
        ILatipayRetryEligibilityService latipayRetryEligibilityService,
        ILatipaySubPaymentMethodService latipaySubPaymentMethodService,
        IOrderService orderService,
        INopUrlHelper nopUrlHelper,
        IWebHelper webHelper,
        LatipaySettings settings)
    {
        _latipayApiClient = latipayApiClient;
        _logger = logger;
        _locker = locker;
        _addressService = addressService;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _latipayOrderNoteService = latipayOrderNoteService;
        _latipayPaymentAttemptService = latipayPaymentAttemptService;
        _latipayRequestFactory = latipayRequestFactory;
        _latipayRetryEligibilityService = latipayRetryEligibilityService;
        _latipaySubPaymentMethodService = latipaySubPaymentMethodService;
        _orderService = orderService;
        _nopUrlHelper = nopUrlHelper;
        _webHelper = webHelper;
        _settings = settings;
    }

    public async Task<LatipayHostedPaymentStartResult> StartHostedPaymentAsync(int orderId, string selectedSubPaymentMethod, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return new LatipayHostedPaymentStartResult
            {
                Message = "The order could not be loaded for Latipay retry."
            };
        }

        LatipayHostedPaymentStartResult result = null;
        var lockAcquired = await _locker.PerformActionWithLockAsync(BuildRetryLockKey(orderId), RetryLockExpiration, async () =>
        {
            result = await StartHostedPaymentInternalAsync(orderId, selectedSubPaymentMethod, cancellationToken);
        });

        if (!lockAcquired)
        {
            await _logger.WarningAsync(
                $"Latipay retry start was skipped for order #{orderId} because another retry request already holds the order lock.");

            return new LatipayHostedPaymentStartResult
            {
                OrderId = orderId,
                Message = "A Latipay retry is already being started for this order. Please wait a moment and refresh the page."
            };
        }

        return result ?? new LatipayHostedPaymentStartResult
        {
            OrderId = orderId,
            Message = "The Latipay retry could not be started."
        };
    }

    private async Task<LatipayHostedPaymentStartResult> StartHostedPaymentInternalAsync(int orderId, string selectedSubPaymentMethod, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order is null || order.Deleted)
        {
            return new LatipayHostedPaymentStartResult
            {
                OrderId = orderId,
                Message = "The order could not be found for Latipay retry."
            };
        }

        var eligibility = await _latipayRetryEligibilityService.EvaluateAsync(order, cancellationToken);
        if (!eligibility.CanRetry)
        {
            await _latipayOrderNoteService.AddNoteIfAbsentAsync(order,
                $"Latipay retry was blocked for order #{order.CustomOrderNumber ?? order.Id.ToString()} because the order was not in a safe retry state. Reason: {eligibility.Message}");

            return new LatipayHostedPaymentStartResult
            {
                OrderId = order.Id,
                Message = eligibility.Message
            };
        }

        var selectionKey = NormalizeOptional(selectedSubPaymentMethod);
        if (!_latipaySubPaymentMethodService.TryGetEnabledMethod(_settings, selectionKey, out var selectedMethod))
        {
            return new LatipayHostedPaymentStartResult
            {
                OrderId = order.Id,
                Message = "The selected Latipay payment option is no longer available. Please choose one of the enabled options and try again."
            };
        }

        var latestAttempt = await _latipayPaymentAttemptService.GetLatestByOrderIdAsync(order.Id);
        var attemptNumber = await _latipayPaymentAttemptService.GetNextAttemptNumberAsync(order.Id);
        var merchantReference = GenerateMerchantReference(order, attemptNumber);
        var paymentAttempt = new LatipayPaymentAttempt
        {
            OrderId = order.Id,
            AttemptNumber = attemptNumber,
            MerchantReference = merchantReference,
            SelectedSubPaymentMethod = selectedMethod.Key,
            Amount = order.OrderTotal,
            Currency = NormalizeCurrency(order.CustomerCurrencyCode),
            RetryOfPaymentAttemptId = latestAttempt?.Id
        };

        await _latipayPaymentAttemptService.InsertAsync(paymentAttempt);

        try
        {
            var hostedPaymentUrl = await CreateHostedPaymentUrlAsync(order, paymentAttempt, selectedMethod, cancellationToken);

            paymentAttempt.RedirectCreatedOnUtc = DateTime.UtcNow;
            paymentAttempt.FailureReasonSummary = null;
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);

            var retryType = latestAttempt is null ? "checkout" : "retry";
            await _latipayOrderNoteService.AddNoteAsync(order,
                $"Latipay {retryType} attempt #{paymentAttempt.AttemptNumber} was created for merchant reference '{paymentAttempt.MerchantReference}' using '{selectedMethod.DisplayName}'.");
            await _logger.InformationAsync(
                $"Latipay hosted payment attempt #{paymentAttempt.AttemptNumber} started for order #{order.Id} with merchant reference '{paymentAttempt.MerchantReference}'.");

            return new LatipayHostedPaymentStartResult
            {
                Started = true,
                OrderId = order.Id,
                PaymentAttemptId = paymentAttempt.Id,
                MerchantReference = paymentAttempt.MerchantReference,
                HostedPaymentUrl = hostedPaymentUrl,
                Message = "Your Latipay payment session has been created."
            };
        }
        catch (LatipayApiException exception)
        {
            paymentAttempt.FailureReasonSummary = BuildFailureSummary(exception.Message);
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
            await _latipayOrderNoteService.AddNoteAsync(order,
                $"Latipay attempt #{paymentAttempt.AttemptNumber} could not be started for merchant reference '{paymentAttempt.MerchantReference}'. Reason: {exception.Message}");
            await _logger.WarningAsync(
                $"Latipay hosted payment attempt #{paymentAttempt.AttemptNumber} failed for order #{order.Id}. Failure kind: {exception.FailureKind}.",
                exception);

            return new LatipayHostedPaymentStartResult
            {
                OrderId = order.Id,
                PaymentAttemptId = paymentAttempt.Id,
                MerchantReference = paymentAttempt.MerchantReference,
                Message = BuildCustomerMessage(exception)
            };
        }
        catch (Exception exception)
        {
            paymentAttempt.FailureReasonSummary = BuildFailureSummary("An unexpected error occurred while starting the Latipay payment session.");
            await _latipayPaymentAttemptService.UpdateAsync(paymentAttempt);
            await _latipayOrderNoteService.AddNoteAsync(order,
                $"Latipay attempt #{paymentAttempt.AttemptNumber} hit an unexpected error for merchant reference '{paymentAttempt.MerchantReference}'. The order remains pending.");
            await _logger.ErrorAsync(
                $"Latipay hosted payment attempt #{paymentAttempt.AttemptNumber} hit an unexpected error for order #{order.Id}.",
                exception);

            return new LatipayHostedPaymentStartResult
            {
                OrderId = order.Id,
                PaymentAttemptId = paymentAttempt.Id,
                MerchantReference = paymentAttempt.MerchantReference,
                Message = "We couldn't start the Latipay payment session right now. Please try again shortly."
            };
        }
    }

    private string BuildAbsoluteRouteUrl(string routeName, object values = null)
    {
        var storeUri = new Uri(_webHelper.GetStoreLocation());
        return _nopUrlHelper.RouteUrl(routeName, values, storeUri.Scheme, storeUri.Authority);
    }

    private string BuildAbsoluteOrderDetailsUrl(int orderId)
    {
        return BuildAbsoluteRouteUrl(NopRouteNames.Standard.ORDER_DETAILS, new { orderId });
    }

    private static string BuildProductName(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var orderIdentifier = string.IsNullOrWhiteSpace(order.CustomOrderNumber)
            ? order.Id.ToString()
            : order.CustomOrderNumber.Trim();
        var value = $"Order {orderIdentifier}";

        return value.Length <= MaxProductNameLength
            ? value
            : value[..MaxProductNameLength];
    }

    private static string BuildRetryLockKey(int orderId)
    {
        return $"latipay-retry:{orderId}";
    }

    private static string BuildFailureSummary(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "Latipay attempt creation failed."
            : message.Trim();
    }

    private static string BuildCustomerMessage(LatipayApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.FailureKind switch
        {
            LatipayApiFailureKind.Timeout or LatipayApiFailureKind.Transport or LatipayApiFailureKind.HttpStatus =>
                "We couldn't connect to Latipay right now. Please try again shortly.",
            LatipayApiFailureKind.Configuration =>
                "Latipay is not configured correctly for this store right now. Please contact support if the problem continues.",
            _ => "We couldn't start the Latipay payment session right now. Please review the order notes or try again shortly."
        };
    }

    private static string GenerateMerchantReference(Order order, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(order);

        var uniqueToken = Guid.NewGuid().ToString("N")[..12];
        var reference = $"ltp-{order.Id}-{attemptNumber}-{uniqueToken}";
        return reference.Length <= 100
            ? reference
            : reference[..100];
    }

    private static string NormalizeCurrency(string currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? LatipayDefaults.CurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
    }

    private string ResolveIpv4Address(string orderIpAddress)
    {
        if (IPAddress.TryParse(orderIpAddress?.Trim(), out var orderIp)
            && orderIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return orderIp.ToString();
        }

        var currentIp = _webHelper.GetCurrentIpAddress();
        if (IPAddress.TryParse(currentIp?.Trim(), out var requestIp)
            && requestIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return requestIp.ToString();
        }

        return string.Empty;
    }

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private async Task<string> CreateHostedPaymentUrlAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipaySubPaymentMethodOption selectedMethod,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(paymentAttempt);
        ArgumentNullException.ThrowIfNull(selectedMethod);

        return selectedMethod.IntegrationMode switch
        {
            LatipayIntegrationMode.HostedCard => await CreateHostedCardPaymentUrlAsync(order, paymentAttempt, cancellationToken),
            _ => await CreateHostedOnlinePaymentUrlAsync(order, paymentAttempt, selectedMethod, cancellationToken)
        };
    }

    private async Task<string> CreateHostedOnlinePaymentUrlAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        LatipaySubPaymentMethodOption selectedMethod,
        CancellationToken cancellationToken)
    {
        var createRequest = await _latipayRequestFactory.BuildCreateTransactionRequestAsync(new CreateTransactionRequestParameters
        {
            SubPaymentMethodKey = selectedMethod.Key,
            Amount = order.OrderTotal,
            CurrencyCode = paymentAttempt.Currency,
            ReturnUrl = BuildAbsoluteRouteUrl(LatipayDefaults.Route.Return),
            CallbackUrl = BuildAbsoluteRouteUrl(LatipayDefaults.Route.Callback),
            BackPageUrl = BuildAbsoluteOrderDetailsUrl(order.Id),
            MerchantReference = paymentAttempt.MerchantReference,
            CustomerIpAddress = ResolveIpv4Address(order.CustomerIp),
            ProductName = BuildProductName(order)
        }, cancellationToken);

        var createResponse = await _latipayApiClient.CreateTransactionAsync(createRequest, cancellationToken);
        return createResponse.HostedPaymentUrl;
    }

    private async Task<string> CreateHostedCardPaymentUrlAsync(Order order,
        LatipayPaymentAttempt paymentAttempt,
        CancellationToken cancellationToken)
    {
        var cardPayerDetails = await CreateCardPayerDetailsAsync(order, cancellationToken);
        var createRequest = await _latipayRequestFactory.BuildCardCreateTransactionRequestAsync(new CardCreateTransactionRequestParameters
        {
            Amount = order.OrderTotal,
            CurrencyCode = paymentAttempt.Currency,
            MerchantReference = paymentAttempt.MerchantReference,
            ProductName = BuildProductName(order),
            ReturnUrl = BuildAbsoluteRouteUrl(LatipayDefaults.Route.Return, new { merchant_reference = paymentAttempt.MerchantReference }),
            CallbackUrl = BuildAbsoluteRouteUrl(LatipayDefaults.Route.Callback),
            CancelOrderUrl = BuildAbsoluteRouteUrl(LatipayDefaults.Route.Retry, new { orderId = order.Id, message = "The Latipay card payment was cancelled before completion." }),
            Payer = cardPayerDetails
        }, cancellationToken);

        var createResponse = await _latipayApiClient.CreateCardTransactionAsync(createRequest, cancellationToken);
        return createResponse.HostedPaymentUrl;
    }

    private async Task<CardPayerDetails> CreateCardPayerDetailsAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        cancellationToken.ThrowIfCancellationRequested();

        if (order.BillingAddressId <= 0)
        {
            throw new LatipayApiException("Latipay card payments require a billing address on the order.",
                LatipayApiFailureKind.RequestValidation);
        }

        var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
        if (billingAddress is null)
        {
            throw new LatipayApiException("Latipay card payments require a billing address that can be loaded from the order.",
                LatipayApiFailureKind.RequestValidation);
        }

        var country = billingAddress.CountryId.HasValue
            ? await _countryService.GetCountryByIdAsync(billingAddress.CountryId.Value)
            : null;
        var stateProvince = billingAddress.StateProvinceId.HasValue
            ? await _stateProvinceService.GetStateProvinceByIdAsync(billingAddress.StateProvinceId.Value)
            : null;

        var state = !string.IsNullOrWhiteSpace(stateProvince?.Name)
            ? stateProvince.Name
            : billingAddress.County;
        var addressLine = string.IsNullOrWhiteSpace(billingAddress.Address2)
            ? billingAddress.Address1
            : $"{billingAddress.Address1} {billingAddress.Address2}".Trim();

        if (string.IsNullOrWhiteSpace(billingAddress.FirstName)
            || string.IsNullOrWhiteSpace(billingAddress.LastName)
            || string.IsNullOrWhiteSpace(addressLine)
            || string.IsNullOrWhiteSpace(state)
            || string.IsNullOrWhiteSpace(billingAddress.City)
            || string.IsNullOrWhiteSpace(billingAddress.ZipPostalCode)
            || string.IsNullOrWhiteSpace(billingAddress.Email)
            || string.IsNullOrWhiteSpace(billingAddress.PhoneNumber))
        {
            throw new LatipayApiException(
                "Latipay card payments require a complete billing contact profile: first name, last name, address, state/county, city, postcode, email, and phone.",
                LatipayApiFailureKind.RequestValidation);
        }

        return new CardPayerDetails
        {
            FirstName = billingAddress.FirstName,
            LastName = billingAddress.LastName,
            Address = addressLine,
            CountryCode = country?.TwoLetterIsoCode,
            State = state,
            City = billingAddress.City,
            Postcode = billingAddress.ZipPostalCode,
            Email = billingAddress.Email,
            Phone = billingAddress.PhoneNumber
        };
    }
}
