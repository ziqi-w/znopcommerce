using System.Globalization;
using System.Net;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services.Api;
using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Interfaces;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Builds validated and signed Latipay requests.
/// </summary>
public class LatipayRequestFactory : ILatipayRequestFactory
{
    private readonly ILatipaySignatureService _signatureService;
    private readonly ILatipaySubPaymentMethodService _subPaymentMethodService;
    private readonly LatipaySettings _settings;

    public LatipayRequestFactory(ILatipaySignatureService signatureService,
        ILatipaySubPaymentMethodService subPaymentMethodService,
        LatipaySettings settings)
    {
        _signatureService = signatureService;
        _subPaymentMethodService = subPaymentMethodService;
        _settings = settings;
    }

    public Task<CreateTransactionRequest> BuildCreateTransactionRequestAsync(CreateTransactionRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateLegacyConfiguration();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_subPaymentMethodService.TryGetMethod(parameters.SubPaymentMethodKey, out var subPaymentMethod)
            || subPaymentMethod.IntegrationMode != LatipayIntegrationMode.HostedOnline)
        {
            throw CreateException("Unsupported Latipay hosted-online sub-payment method key.",
                LatipayApiFailureKind.RequestValidation);
        }

        ValidateCurrency(parameters.CurrencyCode);
        ValidateAmount(parameters.Amount, nameof(parameters.Amount));
        ValidateRequired(parameters.MerchantReference, nameof(parameters.MerchantReference), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(parameters.ProductName, nameof(parameters.ProductName), LatipayApiFailureKind.RequestValidation);
        ValidateAbsoluteWebUrl(parameters.ReturnUrl, nameof(parameters.ReturnUrl), LatipayApiFailureKind.RequestValidation);
        ValidateAbsoluteWebUrl(parameters.CallbackUrl, nameof(parameters.CallbackUrl), LatipayApiFailureKind.RequestValidation);

        if (!string.IsNullOrWhiteSpace(parameters.BackPageUrl))
            ValidateAbsoluteWebUrl(parameters.BackPageUrl, nameof(parameters.BackPageUrl), LatipayApiFailureKind.RequestValidation);

        ValidateIpv4Address(parameters.CustomerIpAddress, nameof(parameters.CustomerIpAddress));

        var request = new CreateTransactionRequest
        {
            UserId = _settings.UserId.Trim(),
            WalletId = _settings.WalletId.Trim(),
            PaymentMethod = subPaymentMethod.ProviderValue,
            Amount = FormatLegacyAmount(parameters.Amount),
            ReturnUrl = parameters.ReturnUrl.Trim(),
            CallbackUrl = parameters.CallbackUrl.Trim(),
            BackPageUrl = NormalizeOptionalValue(parameters.BackPageUrl),
            MerchantReference = parameters.MerchantReference.Trim(),
            Ip = parameters.CustomerIpAddress.Trim(),
            Version = LatipayDefaults.ApiVersion,
            ProductName = parameters.ProductName.Trim(),
            PresentQr = parameters.PresentQr.HasValue ? (parameters.PresentQr.Value ? "1" : "0") : null
        };

        request.Signature = _signatureService.CreateRequestSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.ApiKey);

        return Task.FromResult(request);
    }

    public Task<CardCreateTransactionRequest> BuildCardCreateTransactionRequestAsync(CardCreateTransactionRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateCardConfiguration();
        cancellationToken.ThrowIfCancellationRequested();

        ValidateCurrency(parameters.CurrencyCode);
        ValidateAmount(parameters.Amount, nameof(parameters.Amount));
        ValidateRequired(parameters.MerchantReference, nameof(parameters.MerchantReference), LatipayApiFailureKind.RequestValidation);
        ValidateNoPipe(parameters.MerchantReference, nameof(parameters.MerchantReference));
        ValidateRequired(parameters.ProductName, nameof(parameters.ProductName), LatipayApiFailureKind.RequestValidation);
        ValidateAbsoluteWebUrl(parameters.ReturnUrl, nameof(parameters.ReturnUrl), LatipayApiFailureKind.RequestValidation);
        ValidateAbsoluteWebUrl(parameters.CallbackUrl, nameof(parameters.CallbackUrl), LatipayApiFailureKind.RequestValidation);

        if (!string.IsNullOrWhiteSpace(parameters.CancelOrderUrl))
            ValidateAbsoluteWebUrl(parameters.CancelOrderUrl, nameof(parameters.CancelOrderUrl), LatipayApiFailureKind.RequestValidation);

        ValidatePayer(parameters.Payer);

        var request = new CardCreateTransactionRequest
        {
            MerchantId = _settings.CardMerchantId.Trim(),
            SiteId = ParseCardSiteId(),
            Amount = FormatCardAmount(parameters.Amount),
            OrderId = parameters.MerchantReference.Trim(),
            ProductName = parameters.ProductName.Trim(),
            Currency = LatipayDefaults.CurrencyCode,
            FirstName = parameters.Payer.FirstName.Trim(),
            LastName = parameters.Payer.LastName.Trim(),
            Address = parameters.Payer.Address.Trim(),
            Country = NormalizeCountryCode(parameters.Payer.CountryCode),
            State = parameters.Payer.State.Trim(),
            City = parameters.Payer.City.Trim(),
            Postcode = parameters.Payer.Postcode.Trim(),
            Email = parameters.Payer.Email.Trim(),
            Phone = parameters.Payer.Phone.Trim(),
            Timestamp = GetCurrentUnixTimestampSeconds(),
            ReturnUrl = parameters.ReturnUrl.Trim(),
            CallbackUrl = parameters.CallbackUrl.Trim(),
            CancelOrderUrl = NormalizeOptionalValue(parameters.CancelOrderUrl)
        };

        request.Signature = _signatureService.CreateSortedParameterSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.CardPrivateKey);

        return Task.FromResult(request);
    }

    public Task<QueryTransactionRequest> BuildQueryTransactionRequestAsync(QueryTransactionRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateLegacyConfiguration();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(parameters.MerchantReference, nameof(parameters.MerchantReference), LatipayApiFailureKind.RequestValidation);

        var request = new QueryTransactionRequest
        {
            MerchantReference = parameters.MerchantReference.Trim(),
            UserId = _settings.UserId.Trim(),
            IsBlock = parameters.IsBlock.HasValue ? (parameters.IsBlock.Value ? "1" : "0") : null
        };

        request.Signature = _signatureService.CreateRequestSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.ApiKey);

        return Task.FromResult(request);
    }

    public Task<CardQueryTransactionRequest> BuildCardQueryTransactionRequestAsync(CardQueryTransactionRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateCardConfiguration();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(parameters.MerchantReference, nameof(parameters.MerchantReference), LatipayApiFailureKind.RequestValidation);
        ValidateNoPipe(parameters.MerchantReference, nameof(parameters.MerchantReference));

        var request = new CardQueryTransactionRequest
        {
            MerchantId = _settings.CardMerchantId.Trim(),
            SiteId = ParseCardSiteId(),
            OrderId = parameters.MerchantReference.Trim(),
            Timestamp = GetCurrentUnixTimestampSeconds()
        };

        request.Signature = _signatureService.CreateSortedParameterSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.CardPrivateKey);

        return Task.FromResult(request);
    }

    public Task<RefundRequest> BuildRefundRequestAsync(RefundRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateLegacyConfiguration();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(parameters.OrderId, nameof(parameters.OrderId), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(parameters.Reference, nameof(parameters.Reference), LatipayApiFailureKind.RequestValidation);
        ValidateAmount(parameters.RefundAmount, "refund amount");

        var request = new RefundRequest
        {
            UserId = _settings.UserId.Trim(),
            OrderId = parameters.OrderId.Trim(),
            RefundAmount = FormatLegacyAmount(parameters.RefundAmount),
            Reference = parameters.Reference.Trim()
        };

        request.Signature = _signatureService.CreateRequestSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.ApiKey);

        return Task.FromResult(request);
    }

    public Task<CardRefundRequest> BuildCardRefundRequestAsync(CardRefundRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateCardConfiguration();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequired(parameters.LatipayOrderId, nameof(parameters.LatipayOrderId), LatipayApiFailureKind.RequestValidation);
        ValidateNoPipe(parameters.LatipayOrderId, nameof(parameters.LatipayOrderId));
        ValidateAmount(parameters.RefundAmount, nameof(parameters.RefundAmount));

        var request = new CardRefundRequest
        {
            MerchantId = _settings.CardMerchantId.Trim(),
            SiteId = ParseCardSiteId(),
            RefundAmount = FormatCardAmount(parameters.RefundAmount),
            OrderId = parameters.LatipayOrderId.Trim(),
            Timestamp = GetCurrentUnixTimestampSeconds(),
            Reason = NormalizeOptionalValue(parameters.Reason)
        };

        request.Signature = _signatureService.CreateSortedParameterSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.CardPrivateKey);

        return Task.FromResult(request);
    }

    private void ValidateLegacyConfiguration()
    {
        ValidateRequired(_settings.UserId, nameof(_settings.UserId), LatipayApiFailureKind.Configuration);
        ValidateRequired(_settings.WalletId, nameof(_settings.WalletId), LatipayApiFailureKind.Configuration);
        ValidateRequired(_settings.ApiKey, nameof(_settings.ApiKey), LatipayApiFailureKind.Configuration);
        ValidateAbsoluteWebUrl(_settings.ApiBaseUrl, nameof(_settings.ApiBaseUrl), LatipayApiFailureKind.Configuration);
    }

    private void ValidateCardConfiguration()
    {
        ValidateRequired(_settings.CardMerchantId, nameof(_settings.CardMerchantId), LatipayApiFailureKind.Configuration);
        ValidateRequired(_settings.CardSiteId, nameof(_settings.CardSiteId), LatipayApiFailureKind.Configuration);
        ValidateRequired(_settings.CardPrivateKey, nameof(_settings.CardPrivateKey), LatipayApiFailureKind.Configuration);
        ValidateAbsoluteWebUrl(_settings.CardApiBaseUrl, nameof(_settings.CardApiBaseUrl), LatipayApiFailureKind.Configuration);
        _ = ParseCardSiteId();
    }

    private static void ValidateCurrency(string currencyCode)
    {
        if (!string.Equals(currencyCode?.Trim(), LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateException("Latipay hosted payments are limited to NZD.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateAmount(decimal amount, string parameterName)
    {
        if (amount <= decimal.Zero)
        {
            throw CreateException($"Latipay {parameterName} must be greater than zero.",
                LatipayApiFailureKind.RequestValidation);
        }

        if (decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw CreateException($"Latipay {parameterName} cannot have more than two decimal places.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateRequired(string value, string parameterName, LatipayApiFailureKind failureKind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CreateException($"Latipay {parameterName} must not be empty.", failureKind);
        }
    }

    private static void ValidateAbsoluteWebUrl(string value, string parameterName, LatipayApiFailureKind failureKind)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw CreateException($"Latipay {parameterName} must be an absolute HTTP or HTTPS URL.",
                failureKind);
        }
    }

    private static void ValidateIpv4Address(string value, string parameterName)
    {
        if (!IPAddress.TryParse(value?.Trim(), out var ipAddress)
            || ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw CreateException($"Latipay {parameterName} must be a valid IPv4 address.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateNoPipe(string value, string parameterName)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Contains('|', StringComparison.Ordinal))
        {
            throw CreateException($"Latipay {parameterName} must not contain the '|' character.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidatePayer(CardPayerDetails payer)
    {
        if (payer is null)
        {
            throw CreateException("Latipay payer details are required for hosted card payments.",
                LatipayApiFailureKind.RequestValidation);
        }

        ValidateRequired(payer.FirstName, nameof(payer.FirstName), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.LastName, nameof(payer.LastName), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.Address, nameof(payer.Address), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.State, nameof(payer.State), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.City, nameof(payer.City), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.Postcode, nameof(payer.Postcode), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.Email, nameof(payer.Email), LatipayApiFailureKind.RequestValidation);
        ValidateRequired(payer.Phone, nameof(payer.Phone), LatipayApiFailureKind.RequestValidation);
    }

    private int ParseCardSiteId()
    {
        if (!int.TryParse(_settings.CardSiteId?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var siteId)
            || siteId <= 0)
        {
            throw CreateException("Latipay CardSiteId must be a positive integer.",
                LatipayApiFailureKind.Configuration);
        }

        return siteId;
    }

    private static string FormatLegacyAmount(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatCardAmount(decimal amount)
    {
        return amount.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static long GetCurrentUnixTimestampSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static string NormalizeCountryCode(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        return countryCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeOptionalValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static LatipayApiException CreateException(string message, LatipayApiFailureKind failureKind)
    {
        return new LatipayApiException(message, failureKind);
    }
}
