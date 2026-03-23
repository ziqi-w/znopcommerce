using WS.Plugin.Payments.Latipay.Services.Api.Requests;
using WS.Plugin.Payments.Latipay.Services.Api.Responses;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Represents the Latipay API client abstraction.
/// </summary>
public interface ILatipayApiClient
{
    Task<CreateTransactionResponse> CreateTransactionAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<CardCreateTransactionResponse> CreateCardTransactionAsync(CardCreateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<QueryTransactionResponse> QueryTransactionAsync(QueryTransactionRequest request, CancellationToken cancellationToken = default);

    Task<CardQueryTransactionResponse> QueryCardTransactionAsync(CardQueryTransactionRequest request, CancellationToken cancellationToken = default);

    Task<RefundResponse> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);

    Task<CardRefundResponse> RefundCardTransactionAsync(CardRefundRequest request, CancellationToken cancellationToken = default);
}
