using Nop.Plugin.Payments.Latipay.Domain.Enums;

namespace Nop.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Maps Latipay raw transaction statuses to internal normalized values.
/// </summary>
public interface ILatipayTransactionStatusMapper
{
    LatipayTransactionStatus Normalize(string rawStatus);
}
