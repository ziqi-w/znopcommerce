using WS.Plugin.Misc.GoogleMerchantCenter.Domain.Enums;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Domain;

public sealed class GoogleMerchantFeedRequest
{
    public bool ForceRegeneration { get; init; }

    public int? StoreId { get; init; }

    public int? LanguageId { get; init; }

    public string CurrencyCode { get; init; }

    public string CountryCode { get; init; }
}

public sealed class GoogleMerchantEligibleProduct
{
    public int ProductId { get; init; }

    public int? ProductAttributeCombinationId { get; init; }

    public string ExternalId { get; init; }
}

public sealed class GoogleMerchantEligibilityResult
{
    public IReadOnlyCollection<GoogleMerchantEligibleProduct> Products { get; init; } = Array.Empty<GoogleMerchantEligibleProduct>();

    public int SkippedCount { get; init; }

    public IReadOnlyCollection<GoogleMerchantDiagnosticMessage> Messages { get; init; } = Array.Empty<GoogleMerchantDiagnosticMessage>();
}

public enum GoogleMerchantDiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed class GoogleMerchantDiagnosticMessage
{
    public GoogleMerchantDiagnosticSeverity Severity { get; init; }

    public string Code { get; init; }

    public string Message { get; init; }

    public int? ProductId { get; init; }
}

public sealed class GoogleMerchantDiagnosticsSummary
{
    public DateTime? GeneratedOnUtc { get; init; }

    public string Status { get; init; }

    public string Summary { get; init; }

    public int ExportedItemCount { get; init; }

    public int SkippedItemCount { get; init; }

    public int WarningCount { get; init; }

    public int ErrorCount { get; init; }

    public IReadOnlyCollection<GoogleMerchantDiagnosticMessage> Messages { get; init; } = Array.Empty<GoogleMerchantDiagnosticMessage>();
}

public sealed class GoogleMerchantShippingOption
{
    public string CountryCode { get; init; }

    public string Service { get; init; }

    public decimal Price { get; init; }

    public string CurrencyCode { get; init; }
}

public sealed class GoogleMerchantMappedProduct
{
    public string Id { get; init; }

    public string Title { get; init; }

    public string Description { get; init; }

    public string Link { get; init; }

    public string ImageLink { get; init; }

    public IReadOnlyCollection<string> AdditionalImageLinks { get; init; } = Array.Empty<string>();

    public string Availability { get; init; }

    public decimal? Price { get; init; }

    public decimal? SalePrice { get; init; }

    public string CurrencyCode { get; init; }

    public GoogleMerchantProductCondition Condition { get; init; }

    public string Brand { get; init; }

    public string Gtin { get; init; }

    public string Mpn { get; init; }

    public string GoogleProductCategory { get; init; }

    public string ProductType { get; init; }

    public string ItemGroupId { get; init; }

    public string Color { get; init; }

    public string Size { get; init; }

    public string Material { get; init; }

    public string Pattern { get; init; }

    public string Gender { get; init; }

    public string AgeGroup { get; init; }

    public bool? IdentifierExists { get; init; }

    public IReadOnlyCollection<GoogleMerchantShippingOption> ShippingOptions { get; init; } = Array.Empty<GoogleMerchantShippingOption>();

    public IReadOnlyDictionary<int, string> CustomLabels { get; init; } = new Dictionary<int, string>();
}

public sealed class GoogleMerchantMappingResult
{
    public IReadOnlyCollection<GoogleMerchantMappedProduct> Products { get; init; } = Array.Empty<GoogleMerchantMappedProduct>();

    public int SkippedCount { get; init; }

    public IReadOnlyCollection<GoogleMerchantDiagnosticMessage> Messages { get; init; } = Array.Empty<GoogleMerchantDiagnosticMessage>();
}

public sealed class GoogleMerchantGenerationResult
{
    public bool Succeeded { get; init; }

    public DateTime GeneratedOnUtc { get; init; }

    public string ContentType { get; init; } = GoogleMerchantCenterDefaults.XmlContentType;

    public string FeedContent { get; init; }

    public GoogleMerchantDiagnosticsSummary Diagnostics { get; init; } = new();
}
