using WS.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Services.ScheduleTasks;

namespace WS.Plugin.Payments.Latipay.Services;

/// <summary>
/// Periodically reconciles Latipay attempts.
/// </summary>
public class LatipayReconciliationTask : IScheduleTask
{
    private readonly ILatipayReconciliationService _latipayReconciliationService;

    public LatipayReconciliationTask(ILatipayReconciliationService latipayReconciliationService)
    {
        _latipayReconciliationService = latipayReconciliationService;
    }

    public async Task ExecuteAsync()
    {
        await _latipayReconciliationService.ReconcilePendingAttemptsAsync();
    }
}
