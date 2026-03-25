using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Media;
using WS.Plugin.Misc.AliyunOssStorage.Services;

namespace WS.Plugin.Misc.AliyunOssStorage.Infrastructure;

public class PluginNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IAliyunOssClientFactory, AliyunOssClientFactory>();
        services.AddTransient<IAliyunOssStorageService, AliyunOssStorageService>();
        services.AddTransient<IAliyunOssThumbMigrationService, AliyunOssThumbMigrationService>();
        services.AddTransient<AliyunOssThumbService>();
        services.AddTransient<ThumbService>();
        services.AddTransient<IThumbService>(provider =>
        {
            var settings = provider.GetRequiredService<AliyunOssStorageSettings>();

            if (settings.Enabled && HasRequiredConfiguration(settings))
                return provider.GetRequiredService<AliyunOssThumbService>();

            return provider.GetRequiredService<ThumbService>();
        });
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 3000;

    private static bool HasRequiredConfiguration(AliyunOssStorageSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.Endpoint)
            && !string.IsNullOrWhiteSpace(settings.BucketName)
            && !string.IsNullOrWhiteSpace(settings.Region)
            && !string.IsNullOrWhiteSpace(settings.AccessKeyId)
            && !string.IsNullOrWhiteSpace(settings.AccessKeySecret);
    }
}
