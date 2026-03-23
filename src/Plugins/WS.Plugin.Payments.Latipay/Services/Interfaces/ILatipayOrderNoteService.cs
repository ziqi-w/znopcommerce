using Nop.Core.Domain.Orders;

namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Writes Latipay-specific order notes.
/// </summary>
public interface ILatipayOrderNoteService
{
    Task AddNoteAsync(Order order, string note);

    Task<bool> AddNoteIfAbsentAsync(Order order, string note);
}
