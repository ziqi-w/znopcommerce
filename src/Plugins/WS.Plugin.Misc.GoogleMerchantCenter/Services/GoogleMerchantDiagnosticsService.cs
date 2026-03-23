using System.Text.Json;
using Nop.Services.Configuration;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantDiagnosticsService : IGoogleMerchantDiagnosticsService
{
    private readonly ISettingService _settingService;

    public GoogleMerchantDiagnosticsService(ISettingService settingService)
    {
        _settingService = settingService;
    }

    public async Task<GoogleMerchantDiagnosticsSummary> GetLastSummaryAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();

        return new GoogleMerchantDiagnosticsSummary
        {
            GeneratedOnUtc = settings.LastGenerationUtc,
            Status = settings.LastGenerationStatus,
            Summary = settings.LastGenerationSummary,
            ExportedItemCount = settings.LastGeneratedItemCount,
            SkippedItemCount = settings.LastSkippedItemCount,
            WarningCount = settings.LastWarningCount,
            ErrorCount = settings.LastErrorCount,
            Messages = DeserializeMessages(settings.LastGenerationMessagesJson)
        };
    }

    public async Task SaveGenerationResultAsync(GoogleMerchantGenerationResult result, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(result);

        var settings = await _settingService.LoadSettingAsync<GoogleMerchantCenterSettings>();

        settings.LastGenerationUtc = result.GeneratedOnUtc;
        settings.LastGenerationStatus = result.Diagnostics.Status?.Trim();
        settings.LastGeneratedItemCount = result.Diagnostics.ExportedItemCount;
        settings.LastSkippedItemCount = result.Diagnostics.SkippedItemCount;
        settings.LastWarningCount = result.Diagnostics.WarningCount;
        settings.LastErrorCount = result.Diagnostics.ErrorCount;
        settings.LastGenerationSummary = Truncate(result.Diagnostics.Summary, GoogleMerchantCenterDefaults.MaxSummaryLength);
        settings.LastGenerationMessagesJson = SerializeMessages(result.Diagnostics.Messages);

        await _settingService.SaveSettingAsync(settings);
        await _settingService.ClearCacheAsync();
    }

    private static IReadOnlyCollection<GoogleMerchantDiagnosticMessage> DeserializeMessages(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<GoogleMerchantDiagnosticMessage>();

        try
        {
            return JsonSerializer.Deserialize<List<GoogleMerchantDiagnosticMessage>>(value) ?? new List<GoogleMerchantDiagnosticMessage>();
        }
        catch
        {
            return Array.Empty<GoogleMerchantDiagnosticMessage>();
        }
    }

    private static string SerializeMessages(IReadOnlyCollection<GoogleMerchantDiagnosticMessage> messages)
    {
        if (messages is null || messages.Count == 0)
            return null;

        var persistedMessages = messages
            .Take(GoogleMerchantCenterDefaults.MaxPersistedDiagnosticMessages)
            .Select(message => new GoogleMerchantDiagnosticMessage
            {
                Severity = message.Severity,
                Code = Truncate(message.Code, 100),
                ProductId = message.ProductId,
                Message = Truncate(message.Message, GoogleMerchantCenterDefaults.MaxDiagnosticMessageLength)
            })
            .ToList();

        return JsonSerializer.Serialize(persistedMessages);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}
