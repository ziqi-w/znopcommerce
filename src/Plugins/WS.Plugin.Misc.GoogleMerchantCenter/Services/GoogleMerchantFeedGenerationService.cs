using System.Text;
using System.Xml;
using System.Xml.Linq;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Services.Logging;
using Nop.Services.Stores;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantFeedGenerationService : IGoogleMerchantFeedGenerationService
{
    private readonly IGoogleMerchantDiagnosticsService _diagnosticsService;
    private readonly IGoogleMerchantProductEligibilityService _eligibilityService;
    private readonly ILogger _logger;
    private readonly IGoogleMerchantProductMappingService _mappingService;
    private readonly GoogleMerchantCenterSettings _settings;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly IWebHelper _webHelper;

    public GoogleMerchantFeedGenerationService(IGoogleMerchantDiagnosticsService diagnosticsService,
        IGoogleMerchantProductEligibilityService eligibilityService,
        ILogger logger,
        IGoogleMerchantProductMappingService mappingService,
        GoogleMerchantCenterSettings settings,
        IStoreContext storeContext,
        IStoreService storeService,
        IWebHelper webHelper)
    {
        _diagnosticsService = diagnosticsService;
        _eligibilityService = eligibilityService;
        _logger = logger;
        _mappingService = mappingService;
        _settings = settings;
        _storeContext = storeContext;
        _storeService = storeService;
        _webHelper = webHelper;
    }

    public async Task<GoogleMerchantGenerationResult> GenerateAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var generatedOnUtc = DateTime.UtcNow;

        await _logger.InformationAsync($"{GoogleMerchantCenterDefaults.SystemName} feed generation started.");

        try
        {
            if (_settings.FeedFormat != Domain.Enums.GoogleMerchantFeedFormat.Xml)
            {
                var unsupportedFormatResult = new GoogleMerchantGenerationResult
                {
                    Succeeded = false,
                    GeneratedOnUtc = generatedOnUtc,
                    Diagnostics = new GoogleMerchantDiagnosticsSummary
                    {
                        GeneratedOnUtc = generatedOnUtc,
                        Status = "UnsupportedFormat",
                        Summary = "Only XML feed generation is implemented in the current plugin version.",
                        ErrorCount = 1,
                        Messages = new[]
                        {
                            new GoogleMerchantDiagnosticMessage
                            {
                                Severity = GoogleMerchantDiagnosticSeverity.Error,
                                Code = "UNSUPPORTED_FORMAT",
                                Message = "Tab-delimited output has not been implemented yet. Switch the feed format back to XML."
                            }
                        }
                    }
                };

                await _diagnosticsService.SaveGenerationResultAsync(unsupportedFormatResult, cancellationToken);
                await _logger.WarningAsync($"{GoogleMerchantCenterDefaults.SystemName} feed generation aborted because unsupported format '{_settings.FeedFormat}' was requested.");
                return unsupportedFormatResult;
            }

            var eligibilityResult = await _eligibilityService.GetEligibleProductsAsync(request, cancellationToken);
            var mappingResult = await _mappingService.MapAsync(eligibilityResult.Products, request, cancellationToken);
            var messages = eligibilityResult.Messages.Concat(mappingResult.Messages).ToList();

            foreach (var message in messages)
            {
                var logMessage = message.ProductId.HasValue
                    ? $"[{message.Code}] Product {message.ProductId.Value}: {message.Message}"
                    : $"[{message.Code}] {message.Message}";

                if (message.Severity == GoogleMerchantDiagnosticSeverity.Error)
                    await _logger.InsertLogAsync(LogLevel.Error, $"{GoogleMerchantCenterDefaults.SystemName} feed error", logMessage);
                else if (message.Severity == GoogleMerchantDiagnosticSeverity.Warning)
                    await _logger.InsertLogAsync(LogLevel.Warning, $"{GoogleMerchantCenterDefaults.SystemName} feed warning", logMessage);
            }

            var exportedItemCount = mappingResult.Products.Count;
            var skippedItemCount = eligibilityResult.SkippedCount + mappingResult.SkippedCount;
            var warningCount = messages.Count(message => message.Severity == GoogleMerchantDiagnosticSeverity.Warning);
            var errorCount = messages.Count(message => message.Severity == GoogleMerchantDiagnosticSeverity.Error);
            var result = new GoogleMerchantGenerationResult
            {
                Succeeded = true,
                GeneratedOnUtc = generatedOnUtc,
                FeedContent = await BuildFeedAsync(mappingResult.Products, request, cancellationToken),
                Diagnostics = new GoogleMerchantDiagnosticsSummary
                {
                    GeneratedOnUtc = generatedOnUtc,
                    Status = exportedItemCount > 0 ? "Ready" : "Empty",
                    Summary = BuildSummary(exportedItemCount, skippedItemCount, warningCount, errorCount),
                    ExportedItemCount = exportedItemCount,
                    SkippedItemCount = skippedItemCount,
                    WarningCount = warningCount,
                    ErrorCount = errorCount,
                    Messages = messages
                }
            };

            await _diagnosticsService.SaveGenerationResultAsync(result, cancellationToken);
            await _logger.InformationAsync($"{GoogleMerchantCenterDefaults.SystemName} feed generation completed. Exported {exportedItemCount} item(s), skipped {skippedItemCount}, warnings {warningCount}, errors {errorCount}.");
            return result;
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync($"{GoogleMerchantCenterDefaults.SystemName} feed generation failed.", exception);

            var result = new GoogleMerchantGenerationResult
            {
                Succeeded = false,
                GeneratedOnUtc = generatedOnUtc,
                Diagnostics = new GoogleMerchantDiagnosticsSummary
                {
                    GeneratedOnUtc = generatedOnUtc,
                    Status = "Failed",
                    Summary = "Feed generation failed. Review the system log for the exception details.",
                    ErrorCount = 1,
                    Messages = new[]
                    {
                        new GoogleMerchantDiagnosticMessage
                        {
                            Severity = GoogleMerchantDiagnosticSeverity.Error,
                            Code = "GENERATION_FAILED",
                            Message = exception.Message
                        }
                    }
                }
            };

            await _diagnosticsService.SaveGenerationResultAsync(result, cancellationToken);
            return result;
        }
    }

    private async Task<string> BuildFeedAsync(IReadOnlyCollection<GoogleMerchantMappedProduct> mappedProducts, GoogleMerchantFeedRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var currentStore = request.StoreId.GetValueOrDefault() > 0
            ? await _storeService.GetStoreByIdAsync(request.StoreId.Value)
            : await _storeContext.GetCurrentStoreAsync();
        var storeLocation = ResolveStoreLocation(currentStore);
        XNamespace g = GoogleMerchantCenterDefaults.GoogleNamespace;

        var channel = new XElement("channel",
            new XElement("title", currentStore?.Name ?? GoogleMerchantCenterDefaults.SystemName),
            new XElement("link", storeLocation),
            new XElement("description", "Google Merchant Center product feed"));

        foreach (var product in mappedProducts.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var item = new XElement("item");
            AppendIfHasValue(item, g + "id", product.Id);
            AppendIfHasValue(item, "title", product.Title);
            AppendIfHasValue(item, "description", product.Description);
            AppendIfHasValue(item, "link", product.Link);
            AppendIfHasValue(item, g + "image_link", product.ImageLink);
            AppendIfHasValue(item, g + "availability", product.Availability);
            AppendPriceIfHasValue(item, g + "price", product.Price, product.CurrencyCode);
            AppendPriceIfHasValue(item, g + "sale_price", product.SalePrice, product.CurrencyCode);
            AppendIfHasValue(item, g + "condition", product.Condition.ToString().ToLowerInvariant());
            AppendIfHasValue(item, g + "brand", product.Brand);
            AppendIfHasValue(item, g + "gtin", product.Gtin);
            AppendIfHasValue(item, g + "mpn", product.Mpn);
            AppendIfHasValue(item, g + "google_product_category", product.GoogleProductCategory);
            AppendIfHasValue(item, g + "product_type", product.ProductType);
            AppendIfHasValue(item, g + "item_group_id", product.ItemGroupId);
            AppendIfHasValue(item, g + "color", product.Color);
            AppendIfHasValue(item, g + "size", product.Size);
            AppendIfHasValue(item, g + "material", product.Material);
            AppendIfHasValue(item, g + "pattern", product.Pattern);
            AppendIfHasValue(item, g + "gender", product.Gender);
            AppendIfHasValue(item, g + "age_group", product.AgeGroup);
            AppendBoolIfHasValue(item, g + "identifier_exists", product.IdentifierExists);

            foreach (var additionalImageLink in product.AdditionalImageLinks.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                AppendIfHasValue(item, g + "additional_image_link", additionalImageLink);
            }

            foreach (var shippingOption in product.ShippingOptions)
            {
                var shipping = new XElement(g + "shipping");
                AppendIfHasValue(shipping, g + "country", shippingOption.CountryCode);
                AppendIfHasValue(shipping, g + "service", shippingOption.Service);
                AppendPriceIfHasValue(shipping, g + "price", shippingOption.Price, shippingOption.CurrencyCode);

                if (shipping.HasElements)
                    item.Add(shipping);
            }

            foreach (var customLabel in product.CustomLabels.Where(pair => pair.Key >= 0 && pair.Key <= 4).OrderBy(pair => pair.Key))
            {
                AppendIfHasValue(item, g + $"custom_label_{customLabel.Key}", customLabel.Value);
            }

            channel.Add(item);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "g", g.NamespaceName),
                channel));

        await using var stream = new MemoryStream();
        using (var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = true,
            Encoding = new UTF8Encoding(false),
            Indent = true,
            OmitXmlDeclaration = false
        }))
        {
            document.Save(xmlWriter);
            await xmlWriter.FlushAsync();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void AppendIfHasValue(XContainer parent, XName name, string value)
    {
        var sanitizedValue = SanitizeXmlValue(value);
        if (string.IsNullOrWhiteSpace(sanitizedValue))
            return;

        parent.Add(new XElement(name, sanitizedValue));
    }

    private static void AppendBoolIfHasValue(XContainer parent, XName name, bool? value)
    {
        if (!value.HasValue)
            return;

        parent.Add(new XElement(name, value.Value ? "true" : "false"));
    }

    private static void AppendPriceIfHasValue(XContainer parent, XName name, decimal? value, string currencyCode)
    {
        if (!value.HasValue || string.IsNullOrWhiteSpace(currencyCode))
            return;

        parent.Add(new XElement(name, $"{value.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {currencyCode.Trim().ToUpperInvariant()}"));
    }

    private static string BuildSummary(int exportedItemCount, int skippedItemCount, int warningCount, int errorCount)
    {
        if (exportedItemCount == 0)
            return $"Feed generation completed with no exportable products. Skipped {skippedItemCount} item(s), warnings {warningCount}, errors {errorCount}.";

        return $"Feed generation completed successfully. Exported {exportedItemCount} item(s), skipped {skippedItemCount}, warnings {warningCount}, errors {errorCount}.";
    }

    private string ResolveStoreLocation(Nop.Core.Domain.Stores.Store store)
    {
        if (!string.IsNullOrWhiteSpace(store?.Url))
            return store.Url.Trim().TrimEnd('/') + "/";

        return _webHelper.GetStoreLocation();
    }

    private static string SanitizeXmlValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var sanitizedValue = string.Concat(value.Trim().Where(XmlConvert.IsXmlChar));
        return string.IsNullOrWhiteSpace(sanitizedValue) ? null : sanitizedValue;
    }
}
