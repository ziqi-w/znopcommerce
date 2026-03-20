using Nop.Plugin.Payments.Latipay.Services.Api.Requests;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Builds documented Latipay requests.
/// </summary>
public interface ILatipayRequestFactory
{
    Task<CreateTransactionRequest> BuildCreateTransactionRequestAsync(CreateTransactionRequestParameters parameters, CancellationToken cancellationToken = default);

    Task<CardCreateTransactionRequest> BuildCardCreateTransactionRequestAsync(CardCreateTransactionRequestParameters parameters, CancellationToken cancellationToken = default);

    Task<QueryTransactionRequest> BuildQueryTransactionRequestAsync(QueryTransactionRequestParameters parameters, CancellationToken cancellationToken = default);

    Task<CardQueryTransactionRequest> BuildCardQueryTransactionRequestAsync(CardQueryTransactionRequestParameters parameters, CancellationToken cancellationToken = default);

    Task<RefundRequest> BuildRefundRequestAsync(RefundRequestParameters parameters, CancellationToken cancellationToken = default);

    Task<CardRefundRequest> BuildCardRefundRequestAsync(CardRefundRequestParameters parameters, CancellationToken cancellationToken = default);
}
