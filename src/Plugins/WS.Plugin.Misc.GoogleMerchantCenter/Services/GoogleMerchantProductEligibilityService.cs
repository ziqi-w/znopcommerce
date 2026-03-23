using System.Globalization;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Security;
using Nop.Services.Stores;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantProductEligibilityService : IGoogleMerchantProductEligibilityService
{
    private const string InvalidStoreCode = "INVALID_STORE";
    private const string MissingVariantCombinationsCode = "MISSING_VARIANT_COMBINATIONS";
    private const string ProductAclDeniedCode = "PRODUCT_ACL_DENIED";
    private const string ProductNotAvailableCode = "PRODUCT_NOT_AVAILABLE";
    private const string ProductNotPublishedCode = "PRODUCT_NOT_PUBLISHED";
    private const string StoreNotAllowedCode = "STORE_NOT_ALLOWED";

    private readonly IAclService _aclService;
    private readonly ICustomerService _customerService;
    private readonly GoogleMerchantCenterSettings _settings;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IProductService _productService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreMappingService _storeMappingService;

    public GoogleMerchantProductEligibilityService(IAclService aclService,
        ICustomerService customerService,
        GoogleMerchantCenterSettings settings,
        IProductAttributeService productAttributeService,
        IProductService productService,
        IStoreContext storeContext,
        IStoreMappingService storeMappingService)
    {
        _aclService = aclService;
        _customerService = customerService;
        _settings = settings;
        _productAttributeService = productAttributeService;
        _productService = productService;
        _storeContext = storeContext;
        _storeMappingService = storeMappingService;
    }

    public async Task<GoogleMerchantEligibilityResult> GetEligibleProductsAsync(GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(request);

        var effectiveStoreId = await ResolveStoreIdAsync(request.StoreId);
        if (request.StoreId.GetValueOrDefault() > 0 && effectiveStoreId <= 0)
        {
            return new GoogleMerchantEligibilityResult
            {
                Messages = new[]
                {
                    CreateMessage(GoogleMerchantDiagnosticSeverity.Error, InvalidStoreCode,
                        $"Store {request.StoreId.Value} could not be resolved for feed generation.")
                }
            };
        }

        if (!IsStoreAllowed(effectiveStoreId))
        {
            return new GoogleMerchantEligibilityResult
            {
                Messages = new[]
                {
                    CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, StoreNotAllowedCode,
                        effectiveStoreId > 0
                            ? $"Store {effectiveStoreId} is outside the plugin's configured store scope."
                            : "The current store is outside the plugin's configured store scope.")
                }
            };
        }

        var products = await _productService.SearchProductsAsync(
            storeId: effectiveStoreId,
            productType: ProductType.SimpleProduct,
            visibleIndividuallyOnly: true,
            showHidden: true);
        var publicCustomer = await _customerService.GetOrCreateSearchEngineUserAsync();

        var eligibleProducts = new List<GoogleMerchantEligibleProduct>();
        var messages = new List<GoogleMerchantDiagnosticMessage>();
        var skippedCount = 0;

        foreach (var product in products)
        {
            if (!_settings.IncludeUnpublishedProducts && !product.Published)
            {
                skippedCount++;
                messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, ProductNotPublishedCode,
                    "The product is unpublished and unpublished export is disabled.", product.Id));
                continue;
            }

            if (!_productService.ProductIsAvailable(product))
            {
                skippedCount++;
                messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, ProductNotAvailableCode,
                    "The product is outside its availability window and was skipped.", product.Id));
                continue;
            }

            if (effectiveStoreId > 0 && !await _storeMappingService.AuthorizeAsync(product, effectiveStoreId))
            {
                skippedCount++;
                messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, StoreNotAllowedCode,
                    $"The product is not mapped to store {effectiveStoreId} and was skipped.", product.Id));
                continue;
            }

            if (!await _aclService.AuthorizeAsync(product, publicCustomer))
            {
                skippedCount++;
                messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, ProductAclDeniedCode,
                    "The product is ACL-restricted for the public feed context and was skipped.", product.Id));
                continue;
            }

            if (product.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes)
            {
                var combinations = await _productAttributeService.GetAllProductAttributeCombinationsAsync(product.Id);
                var activeCombinations = combinations
                    .Where(combination => !string.IsNullOrWhiteSpace(combination.AttributesXml))
                    .OrderBy(combination => combination.Id)
                    .ToList();

                if (activeCombinations.Count == 0)
                {
                    skippedCount++;
                    messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, MissingVariantCombinationsCode,
                        "The product manages inventory by attribute combinations but has no exportable combinations.", product.Id));
                    continue;
                }

                foreach (var combination in activeCombinations)
                {
                    eligibleProducts.Add(new GoogleMerchantEligibleProduct
                    {
                        ProductId = product.Id,
                        ProductAttributeCombinationId = combination.Id,
                        ExternalId = string.IsNullOrWhiteSpace(combination.Sku)
                            ? $"{product.Id}-{combination.Id}"
                            : combination.Sku.Trim()
                    });
                }

                continue;
            }

            eligibleProducts.Add(new GoogleMerchantEligibleProduct
            {
                ProductId = product.Id,
                ExternalId = product.Id.ToString(CultureInfo.InvariantCulture)
            });
        }

        return new GoogleMerchantEligibilityResult
        {
            Products = eligibleProducts,
            SkippedCount = skippedCount,
            Messages = messages
        };
    }

    private async Task<int> ResolveStoreIdAsync(int? requestedStoreId)
    {
        if (requestedStoreId.GetValueOrDefault() > 0)
            return requestedStoreId.Value;

        return (await _storeContext.GetCurrentStoreAsync())?.Id ?? 0;
    }

    private bool IsStoreAllowed(int storeId)
    {
        var allowedStoreIds = ParseStoreIds(_settings.LimitedToStoreIdsCsv);
        if (allowedStoreIds.Count == 0)
            return true;

        return storeId > 0 && allowedStoreIds.Contains(storeId);
    }

    private static HashSet<int> ParseStoreIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new HashSet<int>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(storeId => int.TryParse(storeId, out var parsedStoreId) ? (int?)parsedStoreId : null)
            .Where(storeId => storeId.HasValue && storeId.Value > 0)
            .Select(storeId => storeId!.Value)
            .ToHashSet();
    }

    private static GoogleMerchantDiagnosticMessage CreateMessage(GoogleMerchantDiagnosticSeverity severity, string code, string message, int? productId = null)
    {
        return new GoogleMerchantDiagnosticMessage
        {
            Severity = severity,
            Code = code,
            Message = message,
            ProductId = productId
        };
    }
}
