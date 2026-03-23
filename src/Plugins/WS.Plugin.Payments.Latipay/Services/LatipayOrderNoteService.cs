using Nop.Core.Domain.Orders;
using Nop.Services.Orders;
using WS.Plugin.Payments.Latipay.Services.Interfaces;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Writes Latipay-specific order notes.
/// </summary>
public class LatipayOrderNoteService : ILatipayOrderNoteService
{
    private readonly IOrderService _orderService;

    public LatipayOrderNoteService(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task AddNoteAsync(Order order, string note)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note.Trim(),
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    public async Task<bool> AddNoteIfAbsentAsync(Order order, string note)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        var normalizedNote = note.Trim();
        var existingNotes = await _orderService.GetOrderNotesByOrderIdAsync(order.Id, displayToCustomer: false);
        if (existingNotes.Any(orderNote => string.Equals(orderNote.Note?.Trim(), normalizedNote, StringComparison.Ordinal)))
            return false;

        await AddNoteAsync(order, normalizedNote);
        return true;
    }
}
