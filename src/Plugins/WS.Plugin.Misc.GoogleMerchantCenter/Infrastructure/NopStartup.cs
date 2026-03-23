using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using WS.Plugin.Misc.GoogleMerchantCenter.Factories;
using WS.Plugin.Misc.GoogleMerchantCenter.Services;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GoogleMerchantCenterModelFactory>();
        services.AddScoped<IGoogleMerchantFeedAccessService, GoogleMerchantFeedAccessService>();
        services.AddScoped<IGoogleMerchantDiagnosticsService, GoogleMerchantDiagnosticsService>();
        services.AddScoped<IGoogleMerchantFeedGenerationService, GoogleMerchantFeedGenerationService>();
        services.AddScoped<IGoogleMerchantFeedSnapshotService, GoogleMerchantFeedSnapshotService>();
        services.AddScoped<IGoogleMerchantProductEligibilityService, GoogleMerchantProductEligibilityService>();
        services.AddScoped<IGoogleMerchantProductMappingService, GoogleMerchantProductMappingService>();
        services.AddScoped<IGoogleMerchantScheduleTaskService, GoogleMerchantScheduleTaskService>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 1;
}
