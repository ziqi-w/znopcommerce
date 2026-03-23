using FluentAssertions;
using WS.Plugin.Payments.Latipay.Domain.Enums;
using WS.Plugin.Payments.Latipay.Services;
using NUnit.Framework;

namespace Nop.Tests.WS.Plugin.Payments.Latipay.Tests.Services;

[TestFixture]
public class LatipayTransactionStatusMapperTests
{
    private LatipayTransactionStatusMapper _mapper;

    [SetUp]
    public void SetUp()
    {
        _mapper = new LatipayTransactionStatusMapper();
    }

    [TestCase("pending", LatipayTransactionStatus.Pending)]
    [TestCase("paid", LatipayTransactionStatus.Paid)]
    [TestCase("failed", LatipayTransactionStatus.Failed)]
    [TestCase("cancel_or_fail", LatipayTransactionStatus.Failed)]
    [TestCase("canceled", LatipayTransactionStatus.Canceled)]
    [TestCase("cancelled", LatipayTransactionStatus.Canceled)]
    [TestCase("rejected", LatipayTransactionStatus.Rejected)]
    [TestCase("unknown-status", LatipayTransactionStatus.Unknown)]
    [TestCase("", LatipayTransactionStatus.Unknown)]
    public void Normalize_Should_Map_Documented_Statuses(string rawStatus, LatipayTransactionStatus expectedStatus)
    {
        _mapper.Normalize(rawStatus).Should().Be(expectedStatus);
    }
}
