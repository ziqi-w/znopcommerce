using System.Globalization;
using System.Text.RegularExpressions;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Vendors;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Services.Vendors;
using Nop.Web.Framework.Mvc.Routing;
using WS.Plugin.Misc.GoogleMerchantCenter.Domain;
using WS.Plugin.Misc.GoogleMerchantCenter.Services.Interfaces;

namespace WS.Plugin.Misc.GoogleMerchantCenter.Services;

public class GoogleMerchantProductMappingService : IGoogleMerchantProductMappingService
{
    private const string DuplicateFeedIdCode = "DUPLICATE_FEED_ID";
    private const string InvalidVariantCombinationCode = "INVALID_VARIANT_COMBINATION";
    private const string MissingIdentifiersCode = "MISSING_IDENTIFIERS";
    private const string ProductNoAvailabilityCode = "PRODUCT_NO_AVAILABILITY";
    private const string ProductNoDescriptionCode = "PRODUCT_NO_DESCRIPTION";
    private const string ProductNoImageCode = "PRODUCT_NO_IMAGE";
    private const string ProductNoPriceCode = "PRODUCT_NO_PRICE";
    private const string ProductNoTitleCode = "PRODUCT_NO_TITLE";
    private const string ProductNoUrlCode = "PRODUCT_NO_URL";
    private const string ProductNotAvailableCode = "PRODUCT_NOT_AVAILABLE";
    private const string ProductNotFoundCode = "PRODUCT_NOT_FOUND";
    private const string ProductOutOfStockCode = "PRODUCT_OUT_OF_STOCK";
    private const string UnsupportedDownloadCode = "UNSUPPORTED_DOWNLOAD";
    private const string UnsupportedGiftCardCode = "UNSUPPORTED_GIFT_CARD";
    private const string UnsupportedPricingCode = "UNSUPPORTED_PRICING";
    private const string UnsupportedRentalCode = "UNSUPPORTED_RENTAL";

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly ICategoryService _categoryService;
    private readonly CurrencySettings _currencySettings;
    private readonly ICurrencyService _currencyService;
    private readonly ICustomerService _customerService;
    private readonly IHtmlFormatter _htmlFormatter;
    private readonly ILanguageService _languageService;
    private readonly LocalizationSettings _localizationSettings;
    private readonly ILocalizationService _localizationService;
    private readonly IManufacturerService _manufacturerService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IPictureService _pictureService;
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IProductAttributeParser _productAttributeParser;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IProductService _productService;
    private readonly GoogleMerchantCenterSettings _settings;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly ITaxService _taxService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IVendorService _vendorService;
    private readonly IWebHelper _webHelper;

    public GoogleMerchantProductMappingService(ICategoryService categoryService,
        CurrencySettings currencySettings,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IHtmlFormatter htmlFormatter,
        ILanguageService languageService,
        LocalizationSettings localizationSettings,
        ILocalizationService localizationService,
        IManufacturerService manufacturerService,
        INopUrlHelper nopUrlHelper,
        IPictureService pictureService,
        IPriceCalculationService priceCalculationService,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IProductService productService,
        GoogleMerchantCenterSettings settings,
        IStoreContext storeContext,
        IStoreService storeService,
        ITaxService taxService,
        IUrlRecordService urlRecordService,
        IVendorService vendorService,
        IWebHelper webHelper)
    {
        _categoryService = categoryService;
        _currencySettings = currencySettings;
        _currencyService = currencyService;
        _customerService = customerService;
        _htmlFormatter = htmlFormatter;
        _languageService = languageService;
        _localizationSettings = localizationSettings;
        _localizationService = localizationService;
        _manufacturerService = manufacturerService;
        _nopUrlHelper = nopUrlHelper;
        _pictureService = pictureService;
        _priceCalculationService = priceCalculationService;
        _productAttributeParser = productAttributeParser;
        _productAttributeService = productAttributeService;
        _productService = productService;
        _settings = settings;
        _storeContext = storeContext;
        _storeService = storeService;
        _taxService = taxService;
        _urlRecordService = urlRecordService;
        _vendorService = vendorService;
        _webHelper = webHelper;
    }

    public async Task<GoogleMerchantMappingResult> MapAsync(IReadOnlyCollection<GoogleMerchantEligibleProduct> eligibleProducts, GoogleMerchantFeedRequest request, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(eligibleProducts);
        ArgumentNullException.ThrowIfNull(request);

        if (eligibleProducts.Count == 0)
            return new GoogleMerchantMappingResult();

        var store = await ResolveStoreAsync(request.StoreId);
        var storeLocation = ResolveStoreLocation(store);
        var publicCustomer = await _customerService.GetOrCreateSearchEngineUserAsync();
        var languageId = request.LanguageId.GetValueOrDefault() > 0 ? request.LanguageId : store?.DefaultLanguageId;

        var productIds = eligibleProducts
            .Select(product => product.ProductId)
            .Distinct()
            .ToArray();
        var products = await _productService.GetProductsByIdsAsync(productIds);
        var productById = products.ToDictionary(product => product.Id);

        var combinationById = new Dictionary<int, ProductAttributeCombination>();
        foreach (var combinationId in eligibleProducts
                     .Where(product => product.ProductAttributeCombinationId.HasValue)
                     .Select(product => product.ProductAttributeCombinationId!.Value)
                     .Distinct())
        {
            var combination = await _productAttributeService.GetProductAttributeCombinationByIdAsync(combinationId);
            if (combination is not null)
                combinationById[combinationId] = combination;
        }

        var manufacturersByProduct = await _manufacturerService.GetProductManufacturerIdsAsync(productIds);
        var manufacturerIds = manufacturersByProduct
            .SelectMany(pair => pair.Value)
            .Distinct()
            .ToArray();
        var manufacturerById = manufacturerIds.Length == 0
            ? new Dictionary<int, Manufacturer>()
            : (await _manufacturerService.GetManufacturersByIdsAsync(manufacturerIds)).ToDictionary(manufacturer => manufacturer.Id);
        var vendorById = (await _vendorService.GetVendorsByProductIdsAsync(productIds)).ToDictionary(vendor => vendor.Id);

        var primaryCurrency = await ResolvePrimaryStoreCurrencyAsync();
        var targetCurrency = await ResolveTargetCurrencyAsync(request.CurrencyCode, primaryCurrency);

        var mappedProducts = new List<GoogleMerchantMappedProduct>();
        var messages = new List<GoogleMerchantDiagnosticMessage>();
        var skippedCount = 0;
        var seenExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var eligibleProduct in eligibleProducts.OrderBy(product => product.ExternalId, StringComparer.Ordinal))
        {
            if (!productById.TryGetValue(eligibleProduct.ProductId, out var product))
            {
                skippedCount++;
                messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Error, ProductNotFoundCode,
                    "The product could not be reloaded during feed mapping and was skipped.", eligibleProduct.ProductId));
                continue;
            }

            var mapping = await MapProductAsync(product, eligibleProduct, combinationById, publicCustomer, store, storeLocation, languageId,
                primaryCurrency, targetCurrency, manufacturersByProduct, manufacturerById, vendorById);

            if (mapping.Product is null)
            {
                skippedCount++;
                messages.AddRange(mapping.Messages);
                continue;
            }

            if (!seenExternalIds.Add(mapping.Product.Id))
            {
                skippedCount++;
                messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Error, DuplicateFeedIdCode,
                    $"The feed item id '{mapping.Product.Id}' is duplicated and the later record was skipped.", product.Id));
                continue;
            }

            mappedProducts.Add(mapping.Product);
            messages.AddRange(mapping.Messages);
        }

        return new GoogleMerchantMappingResult
        {
            Products = mappedProducts,
            SkippedCount = skippedCount,
            Messages = messages
        };
    }

    private async Task<(GoogleMerchantMappedProduct Product, IReadOnlyCollection<GoogleMerchantDiagnosticMessage> Messages)> MapProductAsync(Product product,
        GoogleMerchantEligibleProduct eligibleProduct,
        IDictionary<int, ProductAttributeCombination> combinationById,
        Customer customer,
        Store store,
        string storeLocation,
        int? languageId,
        Currency primaryCurrency,
        Currency targetCurrency,
        IDictionary<int, int[]> manufacturersByProduct,
        IDictionary<int, Manufacturer> manufacturerById,
        IDictionary<int, Vendor> vendorById)
    {
        if (product.IsGiftCard)
            return Skip(product.Id, UnsupportedGiftCardCode, "Gift card products are not exported to Google Merchant Center.");

        if (product.IsDownload)
            return Skip(product.Id, UnsupportedDownloadCode, "Downloadable products are not exported by the current feed implementation.");

        if (product.IsRental)
            return Skip(product.Id, UnsupportedRentalCode, "Rental products are not exported by the current feed implementation.");

        if (product.CallForPrice || product.CustomerEntersPrice)
            return Skip(product.Id, UnsupportedPricingCode, "Products without a fixed public price are not exported.");

        if (!_productService.ProductIsAvailable(product))
            return Skip(product.Id, ProductNotAvailableCode, "The product is outside its availability window and was skipped.");

        ProductAttributeCombination combination = null;
        if (eligibleProduct.ProductAttributeCombinationId.HasValue)
        {
            if (!combinationById.TryGetValue(eligibleProduct.ProductAttributeCombinationId.Value, out combination) || combination.ProductId != product.Id)
            {
                return (null, new[]
                {
                    CreateMessage(GoogleMerchantDiagnosticSeverity.Error, InvalidVariantCombinationCode,
                        "The selected product attribute combination could not be reloaded for mapping.", product.Id)
                });
            }
        }

        var variantData = combination is null
            ? VariantSelectionData.Empty
            : await BuildVariantSelectionDataAsync(product, combination, customer, store, languageId);

        var baseTitle = NormalizeText(await _localizationService.GetLocalizedAsync(product, item => item.Name, languageId, true, false));
        var title = BuildTitle(baseTitle, variantData.TitleSuffix);
        if (string.IsNullOrWhiteSpace(title))
            return Skip(product.Id, ProductNoTitleCode, "The product has no exportable title.");

        var description = await BuildDescriptionAsync(product, languageId);
        if (string.IsNullOrWhiteSpace(description))
            return Skip(product.Id, ProductNoDescriptionCode, "The product has no exportable description.");

        var link = await BuildProductUrlAsync(product, storeLocation, languageId);
        if (string.IsNullOrWhiteSpace(link))
            return Skip(product.Id, ProductNoUrlCode, "The product URL could not be resolved to an absolute public URL.");

        var imageResult = await BuildImageUrlsAsync(product, storeLocation, combination?.AttributesXml);
        if (string.IsNullOrWhiteSpace(imageResult.PrimaryImageLink) && !_settings.IncludeProductsWithoutImages)
            return Skip(product.Id, ProductNoImageCode, "The product has no primary image and image-less export is disabled.");

        var availability = await ResolveAvailabilityAsync(product, combination);
        if (string.IsNullOrWhiteSpace(availability))
            return Skip(product.Id, ProductNoAvailabilityCode, "The product availability could not be mapped.");

        if (string.Equals(availability, "out of stock", StringComparison.Ordinal) && !_settings.IncludeOutOfStockProducts)
            return Skip(product.Id, ProductOutOfStockCode, "The product is out of stock and out-of-stock export is disabled.");

        var priceResult = await BuildPriceAsync(product, combination, variantData.AdditionalCharge, customer, store, primaryCurrency, targetCurrency);
        if (!priceResult.Price.HasValue || priceResult.Price.Value <= 0m)
            return Skip(product.Id, ProductNoPriceCode, "The product does not have a valid positive public price.");

        var brand = await ResolveBrandAsync(product, store, languageId, manufacturersByProduct, manufacturerById, vendorById);
        var gtin = ResolveGtin(product, combination, variantData);
        var mpn = ResolveMpn(product, combination, variantData);
        var identifierExists = DetermineIdentifierExists(brand, gtin, mpn);

        var messages = new List<GoogleMerchantDiagnosticMessage>();
        if (identifierExists == false)
        {
            messages.Add(CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, MissingIdentifiersCode,
                "The product has no brand, GTIN, or MPN. identifier_exists=false will be exported.", product.Id));
        }

        return (new GoogleMerchantMappedProduct
        {
            Id = NormalizeText(eligibleProduct.ExternalId) ?? (combination is null
                ? product.Id.ToString(CultureInfo.InvariantCulture)
                : $"{product.Id}-{combination.Id}"),
            Title = title,
            Description = description,
            Link = link,
            ImageLink = imageResult.PrimaryImageLink,
            AdditionalImageLinks = imageResult.AdditionalImageLinks,
            Availability = availability,
            Price = priceResult.Price,
            SalePrice = priceResult.SalePrice,
            CurrencyCode = targetCurrency?.CurrencyCode ?? primaryCurrency?.CurrencyCode ?? _settings.DefaultCurrencyCode,
            Condition = _settings.DefaultCondition,
            Brand = brand,
            Gtin = gtin,
            Mpn = mpn,
            ProductType = await BuildProductTypeAsync(product, languageId),
            ItemGroupId = combination is not null
                ? product.Id.ToString(CultureInfo.InvariantCulture)
                : product.ParentGroupedProductId > 0
                    ? product.ParentGroupedProductId.ToString(CultureInfo.InvariantCulture)
                    : null,
            Color = variantData.Color,
            Size = variantData.Size,
            Material = variantData.Material,
            Pattern = variantData.Pattern,
            Gender = variantData.Gender,
            AgeGroup = variantData.AgeGroup,
            IdentifierExists = identifierExists,
            ShippingOptions = await BuildShippingOptionsAsync(product, primaryCurrency, targetCurrency)
        }, messages);
    }

    private static string BuildTitle(string baseTitle, string titleSuffix)
    {
        if (string.IsNullOrWhiteSpace(baseTitle))
            return null;

        var title = string.IsNullOrWhiteSpace(titleSuffix)
            ? baseTitle
            : $"{baseTitle} - {titleSuffix}";

        return NormalizeText(title, GoogleMerchantCenterDefaults.MaxTitleLength);
    }

    private async Task<string> BuildDescriptionAsync(Product product, int? languageId)
    {
        var fullDescription = await _localizationService.GetLocalizedAsync(product, item => item.FullDescription, languageId, true, false);
        var shortDescription = await _localizationService.GetLocalizedAsync(product, item => item.ShortDescription, languageId, true, false);
        var rawDescription = !string.IsNullOrWhiteSpace(fullDescription) ? fullDescription : shortDescription;

        if (string.IsNullOrWhiteSpace(rawDescription))
            return null;

        var plainText = _htmlFormatter.StripTags(_htmlFormatter.ConvertHtmlToPlainText(rawDescription, decode: true, replaceAnchorTags: true));
        return NormalizeText(plainText, GoogleMerchantCenterDefaults.MaxDescriptionLength);
    }

    private async Task<string> BuildProductUrlAsync(Product product, string storeLocation, int? languageId)
    {
        var protocol = GetPreferredProtocol(storeLocation);
        var host = GetHost(storeLocation);
        var url = await _nopUrlHelper.RouteGenericUrlAsync(product, protocol, host, null, languageId, false);

        if (string.IsNullOrWhiteSpace(url))
            url = await BuildSlugFallbackUrlAsync(product, storeLocation, languageId);

        return EnsureAbsoluteUrl(url, storeLocation);
    }

    private async Task<string> BuildSlugFallbackUrlAsync(Product product, string storeLocation, int? languageId)
    {
        var seName = NormalizeText(await _urlRecordService.GetSeNameAsync(product, languageId ?? 0, true, false));
        if (string.IsNullOrWhiteSpace(seName))
            return null;

        var segments = new List<string>();
        if (languageId.GetValueOrDefault() > 0 && _localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
        {
            var language = await _languageService.GetLanguageByIdAsync(languageId.Value);
            var seoCode = NormalizeText(language?.UniqueSeoCode);
            if (!string.IsNullOrWhiteSpace(seoCode))
                segments.Add(seoCode);
        }

        segments.Add(seName);
        return EnsureAbsoluteUrl("/" + string.Join('/', segments), storeLocation);
    }

    private async Task<(string PrimaryImageLink, IReadOnlyCollection<string> AdditionalImageLinks)> BuildImageUrlsAsync(Product product, string storeLocation, string attributesXml)
    {
        var primaryPicture = await _pictureService.GetProductPictureAsync(product, attributesXml);
        var primaryImageLink = await GetPictureUrlAsync(primaryPicture, storeLocation);

        if (!_settings.ExportAdditionalImageLinks)
            return (primaryImageLink, Array.Empty<string>());

        var additionalImageLinks = new List<string>();
        var allPictures = await _pictureService.GetPicturesByProductIdAsync(product.Id);

        foreach (var picture in allPictures)
        {
            if (picture?.Id == primaryPicture?.Id)
                continue;

            var url = await GetPictureUrlAsync(picture, storeLocation);
            if (string.IsNullOrWhiteSpace(url))
                continue;

            if (string.Equals(url, primaryImageLink, StringComparison.OrdinalIgnoreCase))
                continue;

            additionalImageLinks.Add(url);
        }

        return (primaryImageLink, additionalImageLinks);
    }

    private async Task<string> ResolveAvailabilityAsync(Product product, ProductAttributeCombination combination)
    {
        if (combination is not null)
        {
            if (combination.StockQuantity > 0)
                return "in stock";

            return combination.AllowOutOfStockOrders || product.BackorderMode != BackorderMode.NoBackorders
                ? "backorder"
                : "out of stock";
        }

        if (product.ManageInventoryMethod == ManageInventoryMethod.DontManageStock)
            return "in stock";

        var stockQuantity = await _productService.GetTotalStockQuantityAsync(product);
        if (stockQuantity > 0)
            return "in stock";

        return product.BackorderMode == BackorderMode.NoBackorders ? "out of stock" : "backorder";
    }

    private async Task<(decimal? Price, decimal? SalePrice)> BuildPriceAsync(Product product,
        ProductAttributeCombination combination,
        decimal additionalCharge,
        Customer customer,
        Store store,
        Currency primaryCurrency,
        Currency targetCurrency)
    {
        var (priceWithoutDiscounts, finalPrice, _, _) = await _priceCalculationService.GetFinalPriceAsync(product, customer, store,
            combination?.OverriddenPrice, additionalCharge, true, 1, null, null);
        var regularPrice = priceWithoutDiscounts > finalPrice ? priceWithoutDiscounts : finalPrice;
        decimal? salePrice = priceWithoutDiscounts > finalPrice ? finalPrice : null;

        regularPrice = (await _taxService.GetProductPriceAsync(product, regularPrice, true, customer)).price;
        if (salePrice.HasValue)
            salePrice = (await _taxService.GetProductPriceAsync(product, salePrice.Value, true, customer)).price;

        if (primaryCurrency != null && targetCurrency != null && primaryCurrency.Id != targetCurrency.Id)
        {
            regularPrice = await _currencyService.ConvertCurrencyAsync(regularPrice, primaryCurrency, targetCurrency);
            if (salePrice.HasValue)
                salePrice = await _currencyService.ConvertCurrencyAsync(salePrice.Value, primaryCurrency, targetCurrency);
        }

        regularPrice = await _priceCalculationService.RoundPriceAsync(regularPrice, targetCurrency);
        if (salePrice.HasValue)
            salePrice = await _priceCalculationService.RoundPriceAsync(salePrice.Value, targetCurrency);

        if (regularPrice <= 0m)
            return (null, null);

        if (salePrice.HasValue && salePrice.Value >= regularPrice)
            salePrice = null;

        return (regularPrice, salePrice);
    }

    private async Task<string> ResolveBrandAsync(Product product,
        Store store,
        int? languageId,
        IDictionary<int, int[]> manufacturersByProduct,
        IDictionary<int, Manufacturer> manufacturerById,
        IDictionary<int, Vendor> vendorById)
    {
        return _settings.BrandFallbackStrategy switch
        {
            Domain.Enums.GoogleMerchantBrandFallbackStrategy.Manufacturer => await ResolveManufacturerBrandAsync(product, languageId, manufacturersByProduct, manufacturerById),
            Domain.Enums.GoogleMerchantBrandFallbackStrategy.Vendor => await ResolveVendorBrandAsync(product, languageId, vendorById),
            Domain.Enums.GoogleMerchantBrandFallbackStrategy.StoreName => store is null
                ? null
                : NormalizeText(await _localizationService.GetLocalizedAsync(store, item => item.Name, languageId, true, false)),
            _ => null
        };
    }

    private async Task<string> ResolveManufacturerBrandAsync(Product product, int? languageId, IDictionary<int, int[]> manufacturersByProduct, IDictionary<int, Manufacturer> manufacturerById)
    {
        if (!manufacturersByProduct.TryGetValue(product.Id, out var manufacturerIds))
            return null;

        foreach (var manufacturerId in manufacturerIds)
        {
            if (!manufacturerById.TryGetValue(manufacturerId, out var manufacturer))
                continue;

            var name = NormalizeText(await _localizationService.GetLocalizedAsync(manufacturer, item => item.Name, languageId, true, false));
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return null;
    }

    private async Task<string> ResolveVendorBrandAsync(Product product, int? languageId, IDictionary<int, Vendor> vendorById)
    {
        if (product.VendorId <= 0 || !vendorById.TryGetValue(product.VendorId, out var vendor))
            return null;

        return NormalizeText(await _localizationService.GetLocalizedAsync(vendor, item => item.Name, languageId, true, false));
    }

    private string ResolveGtin(Product product, ProductAttributeCombination combination, VariantSelectionData variantData)
    {
        var gtin = NormalizeText(combination?.Gtin) ?? NormalizeText(product.Gtin);
        if (!string.IsNullOrWhiteSpace(gtin) || _settings.GtinFallbackStrategy != Domain.Enums.GoogleMerchantGtinFallbackStrategy.GtinAttribute)
            return gtin;

        return NormalizeText(variantData.Gtin);
    }

    private string ResolveMpn(Product product, ProductAttributeCombination combination, VariantSelectionData variantData)
    {
        var mpn = NormalizeText(combination?.ManufacturerPartNumber) ?? NormalizeText(product.ManufacturerPartNumber);
        if (!string.IsNullOrWhiteSpace(mpn) || _settings.MpnFallbackStrategy != Domain.Enums.GoogleMerchantMpnFallbackStrategy.MpnAttribute)
            return mpn;

        return NormalizeText(variantData.Mpn);
    }

    private static bool? DetermineIdentifierExists(string brand, string gtin, string mpn)
    {
        return string.IsNullOrWhiteSpace(brand) && string.IsNullOrWhiteSpace(gtin) && string.IsNullOrWhiteSpace(mpn)
            ? false
            : null;
    }

    private async Task<IReadOnlyCollection<GoogleMerchantShippingOption>> BuildShippingOptionsAsync(Product product, Currency primaryCurrency, Currency targetCurrency)
    {
        if (!_settings.IncludeShipping || !product.IsShipEnabled)
            return Array.Empty<GoogleMerchantShippingOption>();

        var shippingPrice = Math.Max(_settings.DefaultShippingPrice, 0m);

        if (primaryCurrency != null && targetCurrency != null && primaryCurrency.Id != targetCurrency.Id)
            shippingPrice = await _currencyService.ConvertCurrencyAsync(shippingPrice, primaryCurrency, targetCurrency);

        shippingPrice = await _priceCalculationService.RoundPriceAsync(shippingPrice, targetCurrency);

        return new[]
        {
            new GoogleMerchantShippingOption
            {
                CountryCode = NormalizeCode(_settings.DefaultShippingCountryCode) ?? NormalizeCode(_settings.DefaultCountryCode),
                Service = NormalizeText(_settings.DefaultShippingService),
                Price = shippingPrice,
                CurrencyCode = targetCurrency?.CurrencyCode ?? primaryCurrency?.CurrencyCode ?? _settings.DefaultCurrencyCode
            }
        };
    }

    private async Task<Currency> ResolvePrimaryStoreCurrencyAsync()
    {
        return await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
    }

    private async Task<Currency> ResolveTargetCurrencyAsync(string requestedCurrencyCode, Currency primaryCurrency)
    {
        var currencyCode = NormalizeCode(requestedCurrencyCode) ?? NormalizeCode(_settings.DefaultCurrencyCode);
        return await _currencyService.GetCurrencyByCodeAsync(currencyCode) ?? primaryCurrency;
    }

    private async Task<Store> ResolveStoreAsync(int? requestedStoreId)
    {
        if (requestedStoreId.GetValueOrDefault() > 0)
            return await _storeService.GetStoreByIdAsync(requestedStoreId.Value);

        return await _storeContext.GetCurrentStoreAsync();
    }

    private string ResolveStoreLocation(Store store)
    {
        var storeLocation = NormalizeUrlBase(store?.Url);
        if (!string.IsNullOrWhiteSpace(storeLocation))
            return storeLocation;

        return NormalizeUrlBase(_webHelper.GetStoreLocation());
    }

    private async Task<string> BuildProductTypeAsync(Product product, int? languageId)
    {
        var productCategories = await _categoryService.GetProductCategoriesByProductIdAsync(product.Id, showHidden: true);
        var primaryCategory = productCategories
            .OrderBy(productCategory => productCategory.DisplayOrder)
            .ThenBy(productCategory => productCategory.Id)
            .FirstOrDefault();
        if (primaryCategory is null)
            return null;

        var category = await _categoryService.GetCategoryByIdAsync(primaryCategory.CategoryId);
        if (category is null)
            return null;

        var breadcrumb = await _categoryService.GetFormattedBreadCrumbAsync(category, separator: " > ", languageId: languageId.GetValueOrDefault());
        return NormalizeText(breadcrumb, GoogleMerchantCenterDefaults.MaxTitleLength);
    }

    private async Task<VariantSelectionData> BuildVariantSelectionDataAsync(Product product,
        ProductAttributeCombination combination,
        Customer customer,
        Store store,
        int? languageId)
    {
        if (string.IsNullOrWhiteSpace(combination.AttributesXml))
            return VariantSelectionData.Empty;

        var selectedValueNames = new List<string>();
        decimal additionalCharge = 0m;
        string color = null;
        string size = null;
        string material = null;
        string pattern = null;
        string gender = null;
        string ageGroup = null;
        string gtin = null;
        string mpn = null;

        var mappings = await _productAttributeParser.ParseProductAttributeMappingsAsync(combination.AttributesXml);
        foreach (var mapping in mappings.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id))
        {
            var attributeLabel = NormalizeAttributeKey(await ResolveAttributeLabelAsync(mapping, languageId));
            var values = await _productAttributeParser.ParseProductAttributeValuesAsync(combination.AttributesXml, mapping.Id);

            foreach (var value in values.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id))
            {
                var valueName = NormalizeText(await _localizationService.GetLocalizedAsync(value, item => item.Name, languageId, true, false));
                if (string.IsNullOrWhiteSpace(valueName))
                    continue;

                selectedValueNames.Add(valueName);
                additionalCharge += await _priceCalculationService.GetProductAttributeValuePriceAdjustmentAsync(product, value, customer, store,
                    combination.OverriddenPrice ?? product.Price, 1);

                if (color is null && IsAttributeMatch(attributeLabel, "color", "colour"))
                    color = valueName;
                else if (size is null && IsAttributeMatch(attributeLabel, "size"))
                    size = valueName;
                else if (material is null && IsAttributeMatch(attributeLabel, "material", "fabric"))
                    material = valueName;
                else if (pattern is null && IsAttributeMatch(attributeLabel, "pattern", "print"))
                    pattern = valueName;
                else if (gender is null && IsAttributeMatch(attributeLabel, "gender", "sex"))
                    gender = NormalizeAttributeValue(valueName);
                else if (ageGroup is null && IsAttributeMatch(attributeLabel, "age", "age group"))
                    ageGroup = NormalizeAttributeValue(valueName);

                if (gtin is null
                    && _settings.GtinFallbackStrategy == Domain.Enums.GoogleMerchantGtinFallbackStrategy.GtinAttribute
                    && IsAttributeMatch(attributeLabel, "gtin", "ean", "upc", "barcode", "isbn"))
                {
                    gtin = valueName;
                }

                if (mpn is null
                    && _settings.MpnFallbackStrategy == Domain.Enums.GoogleMerchantMpnFallbackStrategy.MpnAttribute
                    && IsAttributeMatch(attributeLabel, "mpn", "part number", "part no", "manufacturer part number"))
                {
                    mpn = valueName;
                }
            }
        }

        return new VariantSelectionData
        {
            TitleSuffix = NormalizeText(string.Join(" / ", selectedValueNames.Distinct(StringComparer.OrdinalIgnoreCase))),
            AdditionalCharge = additionalCharge,
            Color = color,
            Size = size,
            Material = material,
            Pattern = pattern,
            Gender = gender,
            AgeGroup = ageGroup,
            Gtin = gtin,
            Mpn = mpn
        };
    }

    private async Task<string> ResolveAttributeLabelAsync(ProductAttributeMapping mapping, int? languageId)
    {
        var textPrompt = NormalizeText(await _localizationService.GetLocalizedAsync(mapping, item => item.TextPrompt, languageId, true, false));
        if (!string.IsNullOrWhiteSpace(textPrompt))
            return textPrompt;

        var attribute = await _productAttributeService.GetProductAttributeByIdAsync(mapping.ProductAttributeId);
        return attribute is null
            ? null
            : NormalizeText(await _localizationService.GetLocalizedAsync(attribute, item => item.Name, languageId, true, false));
    }

    private static string NormalizeAttributeKey(string value)
    {
        return NormalizeText(value)?.ToLowerInvariant();
    }

    private static string NormalizeAttributeValue(string value)
    {
        return NormalizeText(value)?.ToLowerInvariant();
    }

    private static bool IsAttributeMatch(string attributeLabel, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(attributeLabel))
            return false;

        return keywords.Any(keyword => attributeLabel.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPreferredProtocol(string storeLocation)
    {
        if (Uri.TryCreate(storeLocation, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.Scheme;
        }

        return Uri.UriSchemeHttps;
    }

    private static string GetHost(string storeLocation)
    {
        if (!Uri.TryCreate(storeLocation, UriKind.Absolute, out var uri))
            return null;

        return uri.IsDefaultPort ? uri.Host : uri.Authority;
    }

    private static string NormalizeUrlBase(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return url.Trim().TrimEnd('/') + "/";
    }

    private static string EnsureAbsoluteUrl(string value, string storeLocation)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri.ToString();
        }

        if (string.IsNullOrWhiteSpace(storeLocation) || !Uri.TryCreate(storeLocation, UriKind.Absolute, out var baseUri))
            return null;

        return Uri.TryCreate(baseUri, value.TrimStart('/'), out var relativeUri) ? relativeUri.ToString() : null;
    }

    private async Task<string> GetPictureUrlAsync(Picture picture, string storeLocation)
    {
        if (picture is null)
            return null;

        var (url, _) = await _pictureService.GetPictureUrlAsync(picture, showDefaultPicture: false, storeLocation: storeLocation);
        return EnsureAbsoluteUrl(url, storeLocation);
    }

    private static string NormalizeText(string value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalizedValue = WhitespaceRegex.Replace(value.Trim(), " ");

        if (maxLength.HasValue && normalizedValue.Length > maxLength.Value)
            normalizedValue = normalizedValue[..maxLength.Value].Trim();

        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static (GoogleMerchantMappedProduct Product, IReadOnlyCollection<GoogleMerchantDiagnosticMessage> Messages) Skip(int productId, string code, string message)
    {
        return (null, new[]
        {
            CreateMessage(GoogleMerchantDiagnosticSeverity.Warning, code, message, productId)
        });
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

    private sealed class VariantSelectionData
    {
        public static VariantSelectionData Empty { get; } = new();

        public decimal AdditionalCharge { get; init; }

        public string TitleSuffix { get; init; }

        public string Color { get; init; }

        public string Size { get; init; }

        public string Material { get; init; }

        public string Pattern { get; init; }

        public string Gender { get; init; }

        public string AgeGroup { get; init; }

        public string Gtin { get; init; }

        public string Mpn { get; init; }
    }
}
