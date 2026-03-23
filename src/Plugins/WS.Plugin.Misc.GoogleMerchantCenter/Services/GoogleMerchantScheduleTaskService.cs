using Nop.Core.Domain.ScheduleTasks;
using Nop.Services.Configuration;
using Nop.Services.ScheduleTasks;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantScheduleTaskService : IGoogleMerchantScheduleTaskService
{
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly ISettingService _settingService;

    public GoogleMerchantScheduleTaskService(IScheduleTaskService scheduleTaskService,
        ISettingService settingService)
    {
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
    }

    public async Task EnsureTaskAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();
        var intervalSeconds = Math.Clamp(settings.FeedRegenerationIntervalMinutes,
            GoogleMerchantCenterDefaults.MinFeedRegenerationIntervalMinutes,
            GoogleMerchantCenterDefaults.MaxFeedRegenerationIntervalMinutes) * 60;
        var shouldEnable = settings.Enabled;

        var scheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.ScheduleTaskType);
        var legacyScheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.LegacyScheduleTaskType);

        if (scheduleTask is null)
        {
            if (legacyScheduleTask is null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = GoogleMerchantCenterDefaults.ScheduleTaskName,
                    Type = GoogleMerchantCenterDefaults.ScheduleTaskType,
                    Enabled = shouldEnable,
                    Seconds = intervalSeconds,
                    StopOnError = false,
                    LastEnabledUtc = shouldEnable ? DateTime.UtcNow : null
                });

                return;
            }

            scheduleTask = legacyScheduleTask;
            scheduleTask.Type = GoogleMerchantCenterDefaults.ScheduleTaskType;
        }
        else if (legacyScheduleTask is not null && legacyScheduleTask.Id != scheduleTask.Id)
            await _scheduleTaskService.DeleteTaskAsync(legacyScheduleTask);

        if (!scheduleTask.Enabled && shouldEnable)
            scheduleTask.LastEnabledUtc = DateTime.UtcNow;

        scheduleTask.Name = GoogleMerchantCenterDefaults.ScheduleTaskName;
        scheduleTask.Type = GoogleMerchantCenterDefaults.ScheduleTaskType;
        scheduleTask.Enabled = shouldEnable;
        scheduleTask.Seconds = intervalSeconds;
        scheduleTask.StopOnError = false;

        await _scheduleTaskService.UpdateTaskAsync(scheduleTask);
    }

    public async Task DeleteTaskAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var scheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.ScheduleTaskType);
        if (scheduleTask is not null)
            await _scheduleTaskService.DeleteTaskAsync(scheduleTask);

        var legacyScheduleTask = await _scheduleTaskService.GetTaskByTypeAsync(GoogleMerchantCenterDefaults.LegacyScheduleTaskType);
        if (legacyScheduleTask is not null)
            await _scheduleTaskService.DeleteTaskAsync(legacyScheduleTask);
    }
}
