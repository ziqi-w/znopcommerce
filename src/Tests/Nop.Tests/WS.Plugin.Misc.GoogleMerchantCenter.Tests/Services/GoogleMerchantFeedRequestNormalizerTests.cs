using FluentAssertions;
using NUnit.Framework;
using WS.Plugin.Misc.GoogleMerchantCenter.Services;

namespace Nop.Tests.WS.Plugin.Misc.GoogleMerchantCenter.Tests.Services;

[TestFixture]
public class GoogleMerchantFeedRequestNormalizerTests
{
    [TestCase("nzd", "NZD")]
    [TestCase(" n/z$d ", "NZD")]
    [TestCase("../nzd", "NZD")]
    public void NormalizeCurrencyCode_ShouldStripUnsafeCharacters_AndNormalizeCase(string input, string expected)
    {
        GoogleMerchantFeedRequestNormalizer.NormalizeCurrencyCode(input).Should().Be(expected);
    }

    [TestCase("nz", "NZ")]
    [TestCase(" ../n-z ", "NZ")]
    public void NormalizeCountryCode_ShouldStripUnsafeCharacters_AndNormalizeCase(string input, string expected)
    {
        GoogleMerchantFeedRequestNormalizer.NormalizeCountryCode(input).Should().Be(expected);
    }

    [TestCase("NZ", null)]
    [TestCase("NZDD", null)]
    [TestCase("../", null)]
    public void NormalizeCurrencyCode_ShouldRejectInvalidLength(string input, string expected)
    {
        GoogleMerchantFeedRequestNormalizer.NormalizeCurrencyCode(input).Should().Be(expected);
    }

    [Test]
    public void NormalizeSnapshotSegment_ShouldFallBack_WhenInputDoesNotProduceASafeToken()
    {
        GoogleMerchantFeedRequestNormalizer.NormalizeSnapshotSegment("../", "DEFAULT").Should().Be("DEFAULT");
    }
}
