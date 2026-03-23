using Nop.Data;
using WS.Plugin.Payments.Latipay.Domain;
using WS.Plugin.Payments.Latipay.Services.Interfaces;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Provides repository-backed access to payment attempts.
/// </summary>
public class LatipayPaymentAttemptService : ILatipayPaymentAttemptService
{
    private const int MerchantReferenceLength = 100;
    private const int SelectedSubPaymentMethodLength = 50;
    private const int LatipayOrderIdLength = 100;
    private const int ExternalStatusLength = 100;
    private const int CurrencyLength = 10;
    private const int CallbackIdempotencyKeyLength = 200;
    private const int FailureReasonSummaryLength = 1000;

    private readonly IRepository<LatipayPaymentAttempt> _paymentAttemptRepository;

    public LatipayPaymentAttemptService(IRepository<LatipayPaymentAttempt> paymentAttemptRepository)
    {
        _paymentAttemptRepository = paymentAttemptRepository;
    }

    public async Task<LatipayPaymentAttempt> GetByIdAsync(int id)
    {
        if (id <= 0)
            return null;

        return await _paymentAttemptRepository.GetByIdAsync(id, null);
    }

    public async Task<LatipayPaymentAttempt> GetByMerchantReferenceAsync(string merchantReference)
    {
        merchantReference = NormalizeLookupValue(merchantReference, MerchantReferenceLength);
        if (merchantReference is null)
            return null;

        return (await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.MerchantReference == merchantReference)
            .OrderBy(attempt => attempt.Id)
            .Take(1), null)).FirstOrDefault();
    }

    public async Task<LatipayPaymentAttempt> GetByLatipayOrderIdAsync(string latipayOrderId)
    {
        latipayOrderId = NormalizeLookupValue(latipayOrderId, LatipayOrderIdLength);
        if (latipayOrderId is null)
            return null;

        return (await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.LatipayOrderId == latipayOrderId)
            .OrderByDescending(attempt => attempt.Id)
            .Take(1), null)).FirstOrDefault();
    }

    public async Task<LatipayPaymentAttempt> GetByCallbackIdempotencyKeyAsync(string callbackIdempotencyKey)
    {
        callbackIdempotencyKey = NormalizeLookupValue(callbackIdempotencyKey, CallbackIdempotencyKeyLength);
        if (callbackIdempotencyKey is null)
            return null;

        return (await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.CallbackIdempotencyKey == callbackIdempotencyKey)
            .OrderBy(attempt => attempt.Id)
            .Take(1), null)).FirstOrDefault();
    }

    public async Task<LatipayPaymentAttempt> GetLatestByOrderIdAsync(int orderId)
    {
        if (orderId <= 0)
            return null;

        return (await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.OrderId == orderId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .ThenByDescending(attempt => attempt.Id)
            .Take(1), null)).FirstOrDefault();
    }

    public async Task<IList<LatipayPaymentAttempt>> GetByOrderIdAsync(int orderId)
    {
        if (orderId <= 0)
            return [];

        return await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.OrderId == orderId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .ThenByDescending(attempt => attempt.Id), null);
    }

    public async Task<IList<LatipayPaymentAttempt>> GetByRetryOfPaymentAttemptIdAsync(int retryOfPaymentAttemptId)
    {
        if (retryOfPaymentAttemptId <= 0)
            return [];

        return await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.RetryOfPaymentAttemptId == retryOfPaymentAttemptId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .ThenBy(attempt => attempt.Id), null);
    }

    public async Task<IList<LatipayPaymentAttempt>> GetUnresolvedAttemptsAsync(int maxCount = 100)
    {
        if (maxCount <= 0)
            return [];

        return await _paymentAttemptRepository.GetAllAsync(query => query
            .Where(attempt => attempt.PaymentCompletedOnUtc == null
                && attempt.RedirectCreatedOnUtc != null
                && attempt.MerchantReference != null)
            .OrderBy(attempt => attempt.LastQueriedOnUtc.HasValue)
            .ThenBy(attempt => attempt.LastQueriedOnUtc)
            .ThenBy(attempt => attempt.CreatedOnUtc)
            .Take(maxCount), null);
    }

    public async Task<int> GetNextAttemptNumberAsync(int orderId)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        var latestAttempt = await GetLatestByOrderIdAsync(orderId);
        return (latestAttempt?.AttemptNumber ?? 0) + 1;
    }

    public async Task InsertAsync(LatipayPaymentAttempt paymentAttempt)
    {
        ArgumentNullException.ThrowIfNull(paymentAttempt);

        PrepareForSave(paymentAttempt, isNew: true);
        await _paymentAttemptRepository.InsertAsync(paymentAttempt, false);
    }

    public async Task UpdateAsync(LatipayPaymentAttempt paymentAttempt)
    {
        ArgumentNullException.ThrowIfNull(paymentAttempt);

        if (paymentAttempt.Id <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentAttempt.Id));

        PrepareForSave(paymentAttempt, isNew: false);
        await _paymentAttemptRepository.UpdateAsync(paymentAttempt, false);
    }

    protected virtual void PrepareForSave(LatipayPaymentAttempt paymentAttempt, bool isNew)
    {
        if (paymentAttempt.OrderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentAttempt.OrderId));

        if (paymentAttempt.AttemptNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentAttempt.AttemptNumber));

        if (paymentAttempt.Amount <= decimal.Zero)
            throw new ArgumentOutOfRangeException(nameof(paymentAttempt.Amount));

        if (paymentAttempt.RetryOfPaymentAttemptId.HasValue && paymentAttempt.RetryOfPaymentAttemptId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentAttempt.RetryOfPaymentAttemptId));

        paymentAttempt.MerchantReference = NormalizeRequiredValue(
            paymentAttempt.MerchantReference,
            nameof(paymentAttempt.MerchantReference),
            MerchantReferenceLength);
        paymentAttempt.SelectedSubPaymentMethod = NormalizeOptionalValue(paymentAttempt.SelectedSubPaymentMethod, SelectedSubPaymentMethodLength);
        paymentAttempt.LatipayOrderId = NormalizeOptionalValue(paymentAttempt.LatipayOrderId, LatipayOrderIdLength);
        paymentAttempt.ExternalStatus = NormalizeOptionalValue(paymentAttempt.ExternalStatus, ExternalStatusLength);
        paymentAttempt.Currency = NormalizeRequiredValue(paymentAttempt.Currency, nameof(paymentAttempt.Currency), CurrencyLength)
            .ToUpperInvariant();
        paymentAttempt.CallbackIdempotencyKey = NormalizeOptionalValue(paymentAttempt.CallbackIdempotencyKey, CallbackIdempotencyKeyLength);
        paymentAttempt.FailureReasonSummary = NormalizeOptionalValue(paymentAttempt.FailureReasonSummary, FailureReasonSummaryLength, truncate: true);

        var utcNow = DateTime.UtcNow;
        if (isNew && paymentAttempt.CreatedOnUtc == default)
            paymentAttempt.CreatedOnUtc = utcNow;
        else if (paymentAttempt.CreatedOnUtc == default)
            paymentAttempt.CreatedOnUtc = utcNow;

        paymentAttempt.UpdatedOnUtc = utcNow;
    }

    protected virtual string NormalizeLookupValue(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : null;
    }

    protected virtual string NormalizeRequiredValue(string value, string parameterName, int maxLength)
    {
        value = NormalizeLookupValue(value, maxLength);
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty.", parameterName);

        return value;
    }

    protected virtual string NormalizeOptionalValue(string value, int maxLength, bool truncate = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length <= maxLength)
            return value;

        if (truncate)
            return value[..maxLength];

        throw new ArgumentException($"Value cannot be longer than {maxLength} characters.", nameof(value));
    }
}
