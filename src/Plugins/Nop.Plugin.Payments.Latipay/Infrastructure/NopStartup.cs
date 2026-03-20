using System.Net.Http.Headers;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Latipay.Factories;
using Nop.Plugin.Payments.Latipay.Services;
using Nop.Plugin.Payments.Latipay.Services.Interfaces;
using Nop.Web.Framework.Infrastructure.Extensions;

namespace Nop.Plugin.Payments.Latipay.Infrastructure;

/// <summary>
/// Registers Latipay plugin services.
/// </summary>
public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<ILatipayApiClient, LatipayApiClient>(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(LatipayDefaults.UserAgent);
        }).WithProxy();
        services.AddScoped<LatipayModelFactory>();
        services.AddScoped<ILatipayCallbackService, LatipayCallbackService>();
        services.AddScoped<ILatipayCheckoutService, LatipayCheckoutService>();
        services.AddScoped<ILatipayOrderNoteService, LatipayOrderNoteService>();
        services.AddScoped<ILatipayPaymentAttemptService, LatipayPaymentAttemptService>();
        services.AddScoped<ILatipayReconciliationService, LatipayReconciliationService>();
        services.AddScoped<ILatipayRetryEligibilityService, LatipayRetryEligibilityService>();
        services.AddScoped<ILatipayRefundRecordService, LatipayRefundRecordService>();
        services.AddScoped<ILatipayRefundService, LatipayRefundService>();
        services.AddScoped<ILatipayRequestFactory, LatipayRequestFactory>();
        services.AddScoped<ILatipayReturnService, LatipayReturnService>();
        services.AddScoped<ILatipaySignatureService, LatipaySignatureService>();
        services.AddScoped<ILatipayStateMachine, LatipayStateMachine>();
        services.AddScoped<ILatipaySubPaymentMethodService, LatipaySubPaymentMethodService>();
        services.AddScoped<ILatipayTransactionStatusMapper, LatipayTransactionStatusMapper>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 1;
}
