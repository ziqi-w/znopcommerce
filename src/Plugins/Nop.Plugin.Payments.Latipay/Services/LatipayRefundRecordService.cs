using Nop.Data;
using Nop.Plugin.Payments.Latipay.Domain;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;

namespace Nop.Plugin.Payments.Latipay.Services;

/// <summary>
/// Provides repository-backed access to refund records.
/// </summary>
public class LatipayRefundRecordService : ILatipayRefundRecordService
{
    private const int LatipayOrderIdLength = 100;
    private const int RefundReferenceLength = 100;
    private const int RefundStatusLength = 50;
    private const int ExternalResponseSummaryLength = 1000;

    private readonly IRepository<LatipayRefundRecord> _refundRecordRepository;

    public LatipayRefundRecordService(IRepository<LatipayRefundRecord> refundRecordRepository)
    {
        _refundRecordRepository = refundRecordRepository;
    }

    public async Task<LatipayRefundRecord> GetByIdAsync(int id)
    {
        if (id <= 0)
            return null;

        return await _refundRecordRepository.GetByIdAsync(id, null);
    }

    public async Task<LatipayRefundRecord> GetByRefundReferenceAsync(string refundReference)
    {
        refundReference = NormalizeLookupValue(refundReference, RefundReferenceLength);
        if (refundReference is null)
            return null;

        return (await _refundRecordRepository.GetAllAsync(query => query
            .Where(record => record.RefundReference == refundReference)
            .OrderBy(record => record.Id)
            .Take(1), null)).FirstOrDefault();
    }

    public async Task<IList<LatipayRefundRecord>> GetByOrderIdAsync(int orderId)
    {
        if (orderId <= 0)
            return [];

        return await _refundRecordRepository.GetAllAsync(query => query
            .Where(record => record.OrderId == orderId)
            .OrderByDescending(record => record.RequestedOnUtc)
            .ThenByDescending(record => record.Id), null);
    }

    public async Task<IList<LatipayRefundRecord>> GetByPaymentAttemptIdAsync(int paymentAttemptId)
    {
        if (paymentAttemptId <= 0)
            return [];

        return await _refundRecordRepository.GetAllAsync(query => query
            .Where(record => record.PaymentAttemptId == paymentAttemptId)
            .OrderByDescending(record => record.RequestedOnUtc)
            .ThenByDescending(record => record.Id), null);
    }

    public async Task<decimal> GetTotalRefundAmountAsync(int orderId, params string[] refundStatuses)
    {
        if (orderId <= 0)
            return decimal.Zero;

        var normalizedStatuses = refundStatuses?
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var refundRecords = await GetByOrderIdAsync(orderId);
        if (normalizedStatuses?.Count > 0)
        {
            refundRecords = refundRecords
                .Where(record => !string.IsNullOrWhiteSpace(record.RefundStatus) && normalizedStatuses.Contains(record.RefundStatus))
                .ToList();
        }

        return refundRecords.Sum(record => record.RefundAmount);
    }

    public async Task InsertAsync(LatipayRefundRecord refundRecord)
    {
        ArgumentNullException.ThrowIfNull(refundRecord);

        PrepareForSave(refundRecord, isNew: true);
        await _refundRecordRepository.InsertAsync(refundRecord, false);
    }

    public async Task UpdateAsync(LatipayRefundRecord refundRecord)
    {
        ArgumentNullException.ThrowIfNull(refundRecord);

        if (refundRecord.Id <= 0)
            throw new ArgumentOutOfRangeException(nameof(refundRecord.Id));

        PrepareForSave(refundRecord, isNew: false);
        await _refundRecordRepository.UpdateAsync(refundRecord, false);
    }

    protected virtual void PrepareForSave(LatipayRefundRecord refundRecord, bool isNew)
    {
        if (refundRecord.OrderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(refundRecord.OrderId));

        if (refundRecord.PaymentAttemptId <= 0)
            throw new ArgumentOutOfRangeException(nameof(refundRecord.PaymentAttemptId));

        if (refundRecord.RefundAmount <= decimal.Zero)
            throw new ArgumentOutOfRangeException(nameof(refundRecord.RefundAmount));

        refundRecord.LatipayOrderId = NormalizeOptionalValue(refundRecord.LatipayOrderId, LatipayOrderIdLength);
        refundRecord.RefundReference = NormalizeRequiredValue(
            refundRecord.RefundReference,
            nameof(refundRecord.RefundReference),
            RefundReferenceLength);
        refundRecord.RefundStatus = NormalizeOptionalValue(refundRecord.RefundStatus, RefundStatusLength);
        refundRecord.ExternalResponseSummary = NormalizeOptionalValue(refundRecord.ExternalResponseSummary, ExternalResponseSummaryLength, truncate: true);

        var utcNow = DateTime.UtcNow;
        if (refundRecord.RequestedOnUtc == default)
            refundRecord.RequestedOnUtc = utcNow;

        if (isNew && refundRecord.CreatedOnUtc == default)
            refundRecord.CreatedOnUtc = utcNow;
        else if (refundRecord.CreatedOnUtc == default)
            refundRecord.CreatedOnUtc = utcNow;

        refundRecord.UpdatedOnUtc = utcNow;
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
