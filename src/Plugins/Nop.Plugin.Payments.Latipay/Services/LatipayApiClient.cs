using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Plugin.Payments.Latipay.Services.Api;
using Nop.Plugin.Payments.Latipay.Services.Api.Requests;
using Nop.Plugin.Payments.Latipay.Services.Api.Responses;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.Latipay.Services;

/// <summary>
/// Represents the typed HTTP client for Latipay APIs.
/// </summary>
public class LatipayApiClient : ILatipayApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly ILatipaySignatureService _signatureService;
    private readonly ILatipaySubPaymentMethodService _subPaymentMethodService;
    private readonly ILatipayTransactionStatusMapper _transactionStatusMapper;
    private readonly LatipaySettings _settings;

    public LatipayApiClient(HttpClient httpClient,
        ILogger logger,
        ILatipaySignatureService signatureService,
        ILatipaySubPaymentMethodService subPaymentMethodService,
        ILatipayTransactionStatusMapper transactionStatusMapper,
        LatipaySettings settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _signatureService = signatureService;
        _subPaymentMethodService = subPaymentMethodService;
        _transactionStatusMapper = transactionStatusMapper;
        _settings = settings;
    }

    public async Task<CreateTransactionResponse> CreateTransactionAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await LogDebugAsync("Latipay create transaction request", LatipayLogSanitizer.Sanitize(request));
        ValidateCreateTransactionRequest(request);

        using var httpRequest = CreateRequestMessage(HttpMethod.Post, BuildEndpointUri(LatipayDefaults.ApiPaths.Transaction, _settings.ApiBaseUrl));
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var responseContent = await SendAsync(httpRequest, cancellationToken);
        var response = DeserializeResponse<CreateTransactionResponse>(responseContent, "create transaction");
        if (response.Code != 0)
        {
            await LogWarningAsync("Latipay create transaction provider error",
                JsonSerializer.Serialize(new { code = response.Code, message = response.Message }, JsonOptions));
            throw new LatipayApiException(
                $"Latipay create transaction failed with code '{response.Code}': {response.Message}",
                LatipayApiFailureKind.Provider,
                providerCode: response.Code.ToString(CultureInfo.InvariantCulture));
        }

        ValidateCreateTransactionResponse(response);
        var signatureValid = _signatureService.IsHostedResponseSignatureValid(response.Nonce, response.HostUrl, response.Signature, _settings.ApiKey);
        if (!signatureValid)
        {
            await LogWarningAsync("Latipay create transaction signature validation failed",
                LatipayLogSanitizer.Sanitize(response, signatureValid: false));
            throw new LatipayApiException("Latipay create transaction response signature is invalid.",
                LatipayApiFailureKind.SignatureValidation);
        }

        await LogDebugAsync("Latipay create transaction response", LatipayLogSanitizer.Sanitize(response, signatureValid));
        return response;
    }

    public async Task<CardCreateTransactionResponse> CreateCardTransactionAsync(CardCreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await LogDebugAsync("Latipay card create transaction request", LatipayLogSanitizer.Sanitize(request));
        ValidateCardCreateTransactionRequest(request);

        using var httpRequest = CreateRequestMessage(HttpMethod.Post, BuildEndpointUri(LatipayDefaults.ApiPaths.CardTransaction, _settings.CardApiBaseUrl));
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var responseContent = await SendAsync(httpRequest, cancellationToken);
        var response = DeserializeResponse<CardCreateTransactionResponse>(responseContent, "card create transaction");
        ValidateCardCreateTransactionResponse(response);
        if (!response.IsSuccess)
        {
            await LogWarningAsync("Latipay card create transaction provider error", LatipayLogSanitizer.Sanitize(response));
            throw new LatipayApiException(
                $"Latipay card create transaction failed with code '{response.Code}': {response.Message}",
                LatipayApiFailureKind.Provider,
                providerCode: response.Code);
        }

        await LogDebugAsync("Latipay card create transaction response", LatipayLogSanitizer.Sanitize(response));
        return response;
    }

    public async Task<QueryTransactionResponse> QueryTransactionAsync(QueryTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await LogDebugAsync("Latipay query transaction request", LatipayLogSanitizer.Sanitize(request));
        ValidateQueryTransactionRequest(request);

        var endpoint = QueryHelpers.AddQueryString(
            BuildEndpointUri($"{LatipayDefaults.ApiPaths.Transaction}/{Uri.EscapeDataString(request.MerchantReference)}", _settings.ApiBaseUrl).AbsoluteUri,
            request.SignatureValues
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && !string.Equals(pair.Key, "merchant_reference", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        using var httpRequest = CreateRequestMessage(HttpMethod.Get, endpoint);
        var responseContent = await SendAsync(httpRequest, cancellationToken);
        ThrowIfProviderErrorEnvelope(responseContent, "query transaction");
        var response = DeserializeResponse<QueryTransactionResponse>(responseContent, "query transaction");

        ValidateQueryTransactionResponse(response);
        var signatureValid = _signatureService.IsStatusSignatureValid(
            response.MerchantReference,
            response.PaymentMethod,
            response.Status,
            response.Currency,
            response.Amount,
            response.Signature,
            _settings.ApiKey);

        if (!signatureValid)
        {
            await LogWarningAsync("Latipay query transaction signature validation failed",
                LatipayLogSanitizer.Sanitize(response, signatureValid: false));
            throw new LatipayApiException("Latipay query transaction response signature is invalid.",
                LatipayApiFailureKind.SignatureValidation);
        }

        if (_subPaymentMethodService.TryGetMethodByProviderValue(response.PaymentMethod, out var subPaymentMethod))
        {
            response.NormalizedSubPaymentMethodKey = subPaymentMethod.Key;
            response.NormalizedProviderPaymentMethod = subPaymentMethod.ProviderValue;
        }

        response.NormalizedStatus = _transactionStatusMapper.Normalize(response.Status);

        await LogDebugAsync("Latipay query transaction response", LatipayLogSanitizer.Sanitize(response, signatureValid));
        return response;
    }

    public async Task<CardQueryTransactionResponse> QueryCardTransactionAsync(CardQueryTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await LogDebugAsync("Latipay card query transaction request", LatipayLogSanitizer.Sanitize(request));
        ValidateCardQueryTransactionRequest(request);

        var endpoint = QueryHelpers.AddQueryString(
            BuildEndpointUri(LatipayDefaults.ApiPaths.CardQueryTransaction, _settings.CardApiBaseUrl).AbsoluteUri,
            request.SignatureValues
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        using var httpRequest = CreateRequestMessage(HttpMethod.Get, endpoint);
        var responseContent = await SendAsync(httpRequest, cancellationToken);
        ThrowIfProviderErrorEnvelope(responseContent, "card query transaction");
        var response = DeserializeResponse<CardQueryTransactionResponse>(responseContent, "card query transaction");

        ValidateCardQueryTransactionResponse(response);
        var signatureValid = _signatureService.IsSortedParameterSignatureValid(
            response.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            response.Signature,
            _settings.CardPrivateKey);

        if (!signatureValid)
        {
            await LogWarningAsync("Latipay card query transaction signature validation failed",
                LatipayLogSanitizer.Sanitize(response, signatureValid: false));
            throw new LatipayApiException("Latipay card query transaction response signature is invalid.",
                LatipayApiFailureKind.SignatureValidation);
        }

        await LogDebugAsync("Latipay card query transaction response", LatipayLogSanitizer.Sanitize(response, signatureValid));
        return response;
    }

    public async Task<RefundResponse> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await LogDebugAsync("Latipay refund request", LatipayLogSanitizer.Sanitize(request));
        ValidateRefundRequest(request);

        using var httpRequest = CreateRequestMessage(HttpMethod.Post, BuildEndpointUri(LatipayDefaults.ApiPaths.Refund, _settings.ApiBaseUrl));
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var responseContent = await SendAsync(httpRequest, cancellationToken);
        var response = DeserializeResponse<RefundResponse>(responseContent, "refund");
        ValidateRefundResponse(response);

        if (!response.IsSuccess)
        {
            await LogWarningAsync("Latipay refund provider error", LatipayLogSanitizer.Sanitize(response));
            throw new LatipayApiException(
                $"Latipay refund failed with code '{response.Code}': {response.Message}",
                LatipayApiFailureKind.Provider,
                providerCode: response.Code);
        }

        await LogDebugAsync("Latipay refund response", LatipayLogSanitizer.Sanitize(response));
        return response;
    }

    public async Task<CardRefundResponse> RefundCardTransactionAsync(CardRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await LogDebugAsync("Latipay card refund request", LatipayLogSanitizer.Sanitize(request));
        ValidateCardRefundRequest(request);

        using var httpRequest = CreateRequestMessage(HttpMethod.Post, BuildEndpointUri(LatipayDefaults.ApiPaths.CardRefund, _settings.CardApiBaseUrl));
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var responseContent = await SendAsync(httpRequest, cancellationToken);
        var response = DeserializeResponse<CardRefundResponse>(responseContent, "card refund");
        ValidateCardRefundResponse(response);

        if (!response.IsSuccess)
        {
            await LogWarningAsync("Latipay card refund provider error", LatipayLogSanitizer.Sanitize(response));
            throw new LatipayApiException(
                $"Latipay card refund failed with code '{response.Code}': {response.Message}",
                LatipayApiFailureKind.Provider,
                providerCode: response.Code);
        }

        await LogDebugAsync("Latipay card refund response", LatipayLogSanitizer.Sanitize(response));
        return response;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(GetRequestTimeoutSeconds()));

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
            var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                await LogWarningAsync("Latipay HTTP request failed",
                    LatipayLogSanitizer.SanitizeHttpFailure(response.StatusCode, content));

                var (providerCode, providerMessage) = TryParseProviderError(content);
                throw new LatipayApiException(
                    !string.IsNullOrWhiteSpace(providerMessage)
                        ? $"Latipay HTTP request failed: {providerMessage}"
                        : $"Latipay HTTP request failed with status code {(int)response.StatusCode}.",
                    LatipayApiFailureKind.HttpStatus,
                    isTransient: IsTransientStatusCode(response.StatusCode),
                    providerCode: providerCode,
                    httpStatusCode: response.StatusCode);
            }

            return content;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await LogWarningAsync("Latipay HTTP request timed out", exception.Message);
            throw new LatipayApiException("Latipay HTTP request timed out.",
                LatipayApiFailureKind.Timeout,
                isTransient: true,
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            await LogWarningAsync("Latipay HTTP transport failure", exception.Message);
            throw new LatipayApiException("Latipay HTTP transport failure.",
                LatipayApiFailureKind.Transport,
                isTransient: true,
                innerException: exception);
        }
    }

    private static TResponse DeserializeResponse<TResponse>(string responseContent, string operationName)
    {
        try
        {
            var response = JsonSerializer.Deserialize<TResponse>(responseContent, JsonOptions);
            return response ?? throw new JsonException("The response body was empty.");
        }
        catch (JsonException exception)
        {
            throw new LatipayApiException(
                $"Latipay {operationName} response could not be parsed.",
                LatipayApiFailureKind.ResponseValidation,
                innerException: exception);
        }
    }

    private void ValidateCreateTransactionRequest(CreateTransactionRequest request)
    {
        ValidateConfigurationRequired(_settings.ApiKey, nameof(_settings.ApiKey));
        ValidateRequestRequired(request.UserId, nameof(request.UserId));
        ValidateRequestRequired(request.WalletId, nameof(request.WalletId));
        ValidateRequestRequired(request.PaymentMethod, nameof(request.PaymentMethod));
        ValidateRequestRequired(request.Amount, nameof(request.Amount));
        ValidateRequestRequired(request.ReturnUrl, nameof(request.ReturnUrl));
        ValidateRequestRequired(request.CallbackUrl, nameof(request.CallbackUrl));
        ValidateRequestRequired(request.MerchantReference, nameof(request.MerchantReference));
        ValidateRequestRequired(request.Ip, nameof(request.Ip));
        ValidateRequestRequired(request.Version, nameof(request.Version));
        ValidateRequestRequired(request.ProductName, nameof(request.ProductName));
        ValidateRequestRequired(request.Signature, nameof(request.Signature));

        ValidatePositiveAmount(request.Amount, nameof(request.Amount));
        ValidateAbsoluteHttpUrl(request.ReturnUrl, nameof(request.ReturnUrl), LatipayApiFailureKind.RequestValidation);
        ValidateAbsoluteHttpUrl(request.CallbackUrl, nameof(request.CallbackUrl), LatipayApiFailureKind.RequestValidation);
        ValidateOptionalAbsoluteHttpUrl(request.BackPageUrl, nameof(request.BackPageUrl), LatipayApiFailureKind.RequestValidation);
        ValidateIpv4Address(request.Ip, nameof(request.Ip));

        var expectedSignature = _signatureService.CreateRequestSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.ApiKey);
        if (!_signatureService.AreEqual(expectedSignature, request.Signature))
        {
            throw new LatipayApiException("Latipay create transaction request signature is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateCreateTransactionResponse(CreateTransactionResponse response)
    {
        if (response.Code != 0)
            return;

        ValidateResponseRequired(response.HostUrl, nameof(response.HostUrl));
        ValidateResponseRequired(response.Nonce, nameof(response.Nonce));
        ValidateResponseRequired(response.Signature, nameof(response.Signature));

        if (!Uri.TryCreate(response.HostUrl.Trim(), UriKind.Absolute, out var hostUri)
            || (hostUri.Scheme != Uri.UriSchemeHttps && hostUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new LatipayApiException("Latipay create transaction response host_url is invalid.",
                LatipayApiFailureKind.ResponseValidation);
        }

        if (string.IsNullOrWhiteSpace(response.HostedPaymentUrl))
        {
            throw new LatipayApiException("Latipay create transaction response does not contain a usable hosted redirect URL.",
                LatipayApiFailureKind.ResponseValidation);
        }
    }

    private void ValidateCardCreateTransactionRequest(CardCreateTransactionRequest request)
    {
        ValidateConfigurationRequired(_settings.CardPrivateKey, nameof(_settings.CardPrivateKey));
        ValidateRequestRequired(request.MerchantId, nameof(request.MerchantId));
        ValidateRequestRequired(request.Amount, nameof(request.Amount));
        ValidateRequestRequired(request.OrderId, nameof(request.OrderId));
        ValidateRequestRequired(request.ProductName, nameof(request.ProductName));
        ValidateRequestRequired(request.Currency, nameof(request.Currency));
        ValidateRequestRequired(request.FirstName, nameof(request.FirstName));
        ValidateRequestRequired(request.LastName, nameof(request.LastName));
        ValidateRequestRequired(request.Address, nameof(request.Address));
        ValidateRequestRequired(request.State, nameof(request.State));
        ValidateRequestRequired(request.City, nameof(request.City));
        ValidateRequestRequired(request.Postcode, nameof(request.Postcode));
        ValidateRequestRequired(request.Email, nameof(request.Email));
        ValidateRequestRequired(request.Phone, nameof(request.Phone));
        ValidateRequestRequired(request.ReturnUrl, nameof(request.ReturnUrl));
        ValidateRequestRequired(request.CallbackUrl, nameof(request.CallbackUrl));
        ValidateRequestRequired(request.Signature, nameof(request.Signature));

        if (request.SiteId <= 0)
        {
            throw new LatipayApiException("Latipay card create transaction request site_id is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }

        ValidatePositiveAmount(request.Amount, nameof(request.Amount));
        ValidateAbsoluteHttpUrl(request.ReturnUrl, nameof(request.ReturnUrl), LatipayApiFailureKind.RequestValidation);
        ValidateAbsoluteHttpUrl(request.CallbackUrl, nameof(request.CallbackUrl), LatipayApiFailureKind.RequestValidation);
        ValidateOptionalAbsoluteHttpUrl(request.CancelOrderUrl, nameof(request.CancelOrderUrl), LatipayApiFailureKind.RequestValidation);
        ValidateNoPipe(request.OrderId, nameof(request.OrderId), LatipayApiFailureKind.RequestValidation);

        if (!string.Equals(request.Currency.Trim(), LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new LatipayApiException("Latipay card create transaction request currency is not NZD.",
                LatipayApiFailureKind.RequestValidation);
        }

        var expectedSignature = _signatureService.CreateSortedParameterSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.CardPrivateKey);
        if (!_signatureService.AreEqual(expectedSignature, request.Signature))
        {
            throw new LatipayApiException("Latipay card create transaction request signature is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateCardCreateTransactionResponse(CardCreateTransactionResponse response)
    {
        ValidateResponseRequired(response.Code, nameof(response.Code));
        ValidateResponseRequired(response.Message, nameof(response.Message));
        if (!response.IsSuccess)
            return;

        ValidateResponseRequired(response.HostUrl, nameof(response.HostUrl));
        ValidateResponseRequired(response.Nonce, nameof(response.Nonce));

        if (!Uri.TryCreate(response.HostUrl.Trim(), UriKind.Absolute, out var hostUri)
            || (hostUri.Scheme != Uri.UriSchemeHttps && hostUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new LatipayApiException("Latipay card create transaction response host_url is invalid.",
                LatipayApiFailureKind.ResponseValidation);
        }

        if (string.IsNullOrWhiteSpace(response.HostedPaymentUrl))
        {
            throw new LatipayApiException("Latipay card create transaction response does not contain a usable hosted redirect URL.",
                LatipayApiFailureKind.ResponseValidation);
        }
    }

    private void ValidateQueryTransactionRequest(QueryTransactionRequest request)
    {
        ValidateConfigurationRequired(_settings.ApiKey, nameof(_settings.ApiKey));
        ValidateRequestRequired(request.MerchantReference, nameof(request.MerchantReference));
        ValidateRequestRequired(request.UserId, nameof(request.UserId));
        ValidateRequestRequired(request.Signature, nameof(request.Signature));

        var expectedSignature = _signatureService.CreateRequestSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.ApiKey);
        if (!_signatureService.AreEqual(expectedSignature, request.Signature))
        {
            throw new LatipayApiException("Latipay query transaction request signature is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateQueryTransactionResponse(QueryTransactionResponse response)
    {
        ValidateResponseRequired(response.MerchantReference, nameof(response.MerchantReference));
        ValidateResponseRequired(response.Currency, nameof(response.Currency));
        ValidateResponseRequired(response.Amount, nameof(response.Amount));
        ValidateResponseRequired(response.PaymentMethod, nameof(response.PaymentMethod));
        ValidateResponseRequired(response.Status, nameof(response.Status));
        ValidateResponseRequired(response.Signature, nameof(response.Signature));

        if (!string.Equals(response.Currency.Trim(), LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new LatipayApiException("Latipay query transaction response currency is not NZD.",
                LatipayApiFailureKind.ResponseValidation);
        }

        if (!response.TryGetAmountValue(out var amount) || amount <= decimal.Zero)
        {
            throw new LatipayApiException("Latipay query transaction response amount is invalid.",
                LatipayApiFailureKind.ResponseValidation);
        }
    }

    private void ValidateCardQueryTransactionRequest(CardQueryTransactionRequest request)
    {
        ValidateConfigurationRequired(_settings.CardPrivateKey, nameof(_settings.CardPrivateKey));
        ValidateRequestRequired(request.MerchantId, nameof(request.MerchantId));
        ValidateRequestRequired(request.OrderId, nameof(request.OrderId));
        ValidateRequestRequired(request.Signature, nameof(request.Signature));

        if (request.SiteId <= 0)
        {
            throw new LatipayApiException("Latipay card query transaction request site_id is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }

        ValidateNoPipe(request.OrderId, nameof(request.OrderId), LatipayApiFailureKind.RequestValidation);

        var expectedSignature = _signatureService.CreateSortedParameterSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.CardPrivateKey);
        if (!_signatureService.AreEqual(expectedSignature, request.Signature))
        {
            throw new LatipayApiException("Latipay card query transaction request signature is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private void ValidateCardQueryTransactionResponse(CardQueryTransactionResponse response)
    {
        ValidateResponseRequired(response.Code, nameof(response.Code));
        ValidateResponseRequired(response.Message, nameof(response.Message));
        ValidateResponseRequired(response.OrderId, nameof(response.OrderId));
        ValidateResponseRequired(response.MerchantId, nameof(response.MerchantId));
        ValidateResponseRequired(response.SiteId, nameof(response.SiteId));
        ValidateResponseRequired(response.Currency, nameof(response.Currency));
        ValidateResponseRequired(response.Amount, nameof(response.Amount));
        ValidateResponseRequired(response.Status, nameof(response.Status));
        ValidateResponseRequired(response.Signature, nameof(response.Signature));

        if (!response.IsSuccess)
        {
            throw new LatipayApiException(
                $"Latipay card query transaction failed with code '{response.Code}': {response.Message}",
                LatipayApiFailureKind.Provider,
                providerCode: response.Code);
        }

        if (!string.Equals(response.Currency.Trim(), LatipayDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new LatipayApiException("Latipay card query transaction response currency is not NZD.",
                LatipayApiFailureKind.ResponseValidation);
        }

        if (!response.TryGetAmountValue(out var amount) || amount <= decimal.Zero)
        {
            throw new LatipayApiException("Latipay card query transaction response amount is invalid.",
                LatipayApiFailureKind.ResponseValidation);
        }

        if (!string.Equals(response.MerchantId.Trim(), _settings.CardMerchantId?.Trim(), StringComparison.Ordinal))
        {
            throw new LatipayApiException("Latipay card query transaction response merchant_id does not match the configured merchant.",
                LatipayApiFailureKind.ResponseValidation);
        }

        if (!string.Equals(response.SiteId.Trim(), _settings.CardSiteId?.Trim(), StringComparison.Ordinal))
        {
            throw new LatipayApiException("Latipay card query transaction response site_id does not match the configured site.",
                LatipayApiFailureKind.ResponseValidation);
        }
    }

    private void ValidateRefundRequest(RefundRequest request)
    {
        ValidateConfigurationRequired(_settings.ApiKey, nameof(_settings.ApiKey));
        ValidateRequestRequired(request.UserId, nameof(request.UserId));
        ValidateRequestRequired(request.OrderId, nameof(request.OrderId));
        ValidateRequestRequired(request.RefundAmount, nameof(request.RefundAmount));
        ValidateRequestRequired(request.Reference, nameof(request.Reference));
        ValidateRequestRequired(request.Signature, nameof(request.Signature));
        ValidatePositiveAmount(request.RefundAmount, nameof(request.RefundAmount));

        var expectedSignature = _signatureService.CreateRequestSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.ApiKey);
        if (!_signatureService.AreEqual(expectedSignature, request.Signature))
        {
            throw new LatipayApiException("Latipay refund request signature is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateRefundResponse(RefundResponse response)
    {
        ValidateResponseRequired(response.Code, nameof(response.Code));
        ValidateResponseRequired(response.Message, nameof(response.Message));
    }

    private void ValidateCardRefundRequest(CardRefundRequest request)
    {
        ValidateConfigurationRequired(_settings.CardPrivateKey, nameof(_settings.CardPrivateKey));
        ValidateRequestRequired(request.MerchantId, nameof(request.MerchantId));
        ValidateRequestRequired(request.OrderId, nameof(request.OrderId));
        ValidateRequestRequired(request.RefundAmount, nameof(request.RefundAmount));
        ValidateRequestRequired(request.Signature, nameof(request.Signature));

        if (request.SiteId <= 0)
        {
            throw new LatipayApiException("Latipay card refund request site_id is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }

        ValidatePositiveAmount(request.RefundAmount, nameof(request.RefundAmount));
        ValidateNoPipe(request.OrderId, nameof(request.OrderId), LatipayApiFailureKind.RequestValidation);

        var expectedSignature = _signatureService.CreateSortedParameterSignature(
            request.SignatureValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _settings.CardPrivateKey);
        if (!_signatureService.AreEqual(expectedSignature, request.Signature))
        {
            throw new LatipayApiException("Latipay card refund request signature is invalid.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateCardRefundResponse(CardRefundResponse response)
    {
        ValidateResponseRequired(response.Code, nameof(response.Code));
        ValidateResponseRequired(response.Message, nameof(response.Message));
    }

    private static void ValidateConfigurationRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LatipayApiException($"Latipay required configuration '{fieldName}' is missing.",
                LatipayApiFailureKind.Configuration);
        }
    }

    private static void ValidateRequestRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LatipayApiException($"Latipay request field '{fieldName}' is missing.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateResponseRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LatipayApiException($"Latipay response field '{fieldName}' is missing.",
                LatipayApiFailureKind.ResponseValidation);
        }
    }

    private static void ValidatePositiveAmount(string value, string fieldName)
    {
        if (!decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= decimal.Zero)
        {
            throw new LatipayApiException($"Latipay field '{fieldName}' must contain a positive amount.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateAbsoluteHttpUrl(string value, string fieldName, LatipayApiFailureKind failureKind)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new LatipayApiException($"Latipay field '{fieldName}' must be an absolute HTTP or HTTPS URL.",
                failureKind);
        }
    }

    private static void ValidateOptionalAbsoluteHttpUrl(string value, string fieldName, LatipayApiFailureKind failureKind)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ValidateAbsoluteHttpUrl(value, fieldName, failureKind);
    }

    private static void ValidateIpv4Address(string value, string fieldName)
    {
        if (!IPAddress.TryParse(value?.Trim(), out var ipAddress)
            || ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new LatipayApiException($"Latipay field '{fieldName}' must be a valid IPv4 address.",
                LatipayApiFailureKind.RequestValidation);
        }
    }

    private static void ValidateNoPipe(string value, string fieldName, LatipayApiFailureKind failureKind)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Contains('|', StringComparison.Ordinal))
        {
            throw new LatipayApiException($"Latipay field '{fieldName}' must not contain the '|' character.",
                failureKind);
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return numericStatusCode >= 500 || statusCode == HttpStatusCode.RequestTimeout || statusCode == (HttpStatusCode)429;
    }

    private static (string ProviderCode, string ProviderMessage) TryParseProviderError(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return (null, null);

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            var providerCode = root.TryGetProperty("code", out var codeElement)
                ? codeElement.GetRawText().Trim('"')
                : null;
            var providerMessage = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : root.TryGetProperty("msg", out var msgElement)
                    ? msgElement.GetString()
                    : null;

            return (providerCode, providerMessage);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static void ThrowIfProviderErrorEnvelope(string responseContent, string operationName)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return;

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            if (!root.TryGetProperty("code", out var codeElement))
                return;

            var providerCode = codeElement.GetRawText().Trim('"');
            if (string.Equals(providerCode, "0", StringComparison.OrdinalIgnoreCase))
                return;

            var providerMessage = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : root.TryGetProperty("msg", out var msgElement)
                    ? msgElement.GetString()
                    : "Latipay returned an error response.";

            throw new LatipayApiException(
                $"Latipay {operationName} failed with code '{providerCode}': {providerMessage}",
                LatipayApiFailureKind.Provider,
                providerCode: providerCode);
        }
        catch (JsonException)
        {
        }
    }

    private static HttpRequestMessage CreateRequestMessage(HttpMethod method, string requestUri)
    {
        return CreateRequestMessage(method, new Uri(requestUri, UriKind.Absolute));
    }

    private static HttpRequestMessage CreateRequestMessage(HttpMethod method, Uri requestUri)
    {
        return new HttpRequestMessage(method, requestUri);
    }

    private Uri BuildEndpointUri(string relativePath, string configuredBaseUrl)
    {
        ValidateConfiguredBaseUrl(configuredBaseUrl);

        var normalizedBaseUrl = configuredBaseUrl.Trim().EndsWith("/", StringComparison.Ordinal)
            ? configuredBaseUrl.Trim()
            : $"{configuredBaseUrl.Trim()}/";
        var baseUri = new Uri(normalizedBaseUrl, UriKind.Absolute);
        var normalizedRelativePath = NormalizeRelativePath(baseUri, relativePath);

        return new Uri(baseUri, normalizedRelativePath);
    }

    private static void ValidateConfiguredBaseUrl(string configuredBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseUrl)
            || !Uri.TryCreate(configuredBaseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new LatipayApiException("Latipay API base URL is not configured correctly.",
                LatipayApiFailureKind.Configuration);
        }
    }

    private static string NormalizeRelativePath(Uri baseUri, string relativePath)
    {
        var normalizedRelativePath = relativePath.TrimStart('/');
        if (baseUri.AbsolutePath.TrimEnd('/').EndsWith("/v2", StringComparison.OrdinalIgnoreCase)
            && normalizedRelativePath.StartsWith("v2/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRelativePath["v2/".Length..];
        }

        return normalizedRelativePath;
    }

    private int GetRequestTimeoutSeconds()
    {
        return _settings.RequestTimeoutSeconds >= LatipayDefaults.MinRequestTimeoutSeconds
            ? _settings.RequestTimeoutSeconds
            : LatipayDefaults.DefaultRequestTimeoutSeconds;
    }

    private Task LogDebugAsync(string title, string payload)
    {
        if (!_settings.DebugLogging)
            return Task.CompletedTask;

        return _logger.InformationAsync($"{title}: {payload}");
    }

    private Task LogWarningAsync(string title, string payload)
    {
        return _logger.WarningAsync($"{title}: {payload}");
    }
}
