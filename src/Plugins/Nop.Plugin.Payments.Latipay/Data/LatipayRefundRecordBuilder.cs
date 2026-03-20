using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Payments.Latipay.Domain;

namespace Nop.Plugin.Payments.Latipay.Data;

/// <summary>
/// Maps the refund record entity.
/// </summary>
public class LatipayRefundRecordBuilder : NopEntityBuilder<LatipayRefundRecord>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(LatipayRefundRecord.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(LatipayRefundRecord.PaymentAttemptId)).AsInt32().NotNullable()
            .WithColumn(nameof(LatipayRefundRecord.LatipayOrderId)).AsString(100).Nullable()
            .WithColumn(nameof(LatipayRefundRecord.RefundReference)).AsString(100).NotNullable()
                .Unique("AK_LatipayRefundRecord_RefundReference")
            .WithColumn(nameof(LatipayRefundRecord.RefundAmount)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(LatipayRefundRecord.RefundStatus)).AsString(50).Nullable()
            .WithColumn(nameof(LatipayRefundRecord.RequestedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(LatipayRefundRecord.CompletedOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(LatipayRefundRecord.ExternalResponseSummary)).AsString(1000).Nullable()
            .WithColumn(nameof(LatipayRefundRecord.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(LatipayRefundRecord.UpdatedOnUtc)).AsDateTime2().NotNullable();
    }
}
