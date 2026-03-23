namespace WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

public interface IGoogleMerchantScheduleTaskService
{
    Task EnsureTaskAsync(CancellationToken cancellationToken = default);

    Task DeleteTaskAsync(CancellationToken cancellationToken = default);
}
