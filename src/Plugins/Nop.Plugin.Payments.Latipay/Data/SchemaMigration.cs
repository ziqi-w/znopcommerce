using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Data.Mapping;
using Nop.Plugin.Payments.Latipay.Domain;

namespace Nop.Plugin.Payments.Latipay.Data;

/// <summary>
/// Creates the base plugin schema.
/// </summary>
[NopMigration("2026-03-15 00:00:00", "Payments.Latipay base schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        var paymentAttemptTable = NameCompatibilityManager.GetTableName(typeof(LatipayPaymentAttempt));
        var refundRecordTable = NameCompatibilityManager.GetTableName(typeof(LatipayRefundRecord));

        Create.TableFor<LatipayPaymentAttempt>();
        Create.TableFor<LatipayRefundRecord>();

        const string orderAttemptIndexName = "IX_LatipayAttempt_Order_Attempt";
        if (!Schema.Table(paymentAttemptTable).Index(orderAttemptIndexName).Exists())
        {
            Create.Index(orderAttemptIndexName)
                .OnTable(paymentAttemptTable)
                .OnColumn(nameof(LatipayPaymentAttempt.OrderId)).Ascending()
                .OnColumn(nameof(LatipayPaymentAttempt.AttemptNumber)).Ascending()
                .WithOptions().Unique();
        }

        const string orderCreatedIndexName = "IX_LatipayAttempt_Order_Created";
        if (!Schema.Table(paymentAttemptTable).Index(orderCreatedIndexName).Exists())
        {
            Create.Index(orderCreatedIndexName)
                .OnTable(paymentAttemptTable)
                .OnColumn(nameof(LatipayPaymentAttempt.OrderId)).Ascending()
                .OnColumn(nameof(LatipayPaymentAttempt.CreatedOnUtc)).Descending();
        }

        const string latipayOrderIndexName = "IX_LatipayAttempt_LatipayOrderId";
        if (!Schema.Table(paymentAttemptTable).Index(latipayOrderIndexName).Exists())
        {
            Create.Index(latipayOrderIndexName)
                .OnTable(paymentAttemptTable)
                .OnColumn(nameof(LatipayPaymentAttempt.LatipayOrderId)).Ascending();
        }

        const string callbackIdempotencyIndexName = "IX_LatipayAttempt_CallbackKey";
        if (!Schema.Table(paymentAttemptTable).Index(callbackIdempotencyIndexName).Exists())
        {
            Create.Index(callbackIdempotencyIndexName)
                .OnTable(paymentAttemptTable)
                .OnColumn(nameof(LatipayPaymentAttempt.CallbackIdempotencyKey)).Ascending();
        }

        const string retryOfAttemptIndexName = "IX_LatipayAttempt_RetryOf";
        if (!Schema.Table(paymentAttemptTable).Index(retryOfAttemptIndexName).Exists())
        {
            Create.Index(retryOfAttemptIndexName)
                .OnTable(paymentAttemptTable)
                .OnColumn(nameof(LatipayPaymentAttempt.RetryOfPaymentAttemptId)).Ascending();
        }

        const string refundOrderRequestedIndexName = "IX_LatipayRefund_Order_Requested";
        if (!Schema.Table(refundRecordTable).Index(refundOrderRequestedIndexName).Exists())
        {
            Create.Index(refundOrderRequestedIndexName)
                .OnTable(refundRecordTable)
                .OnColumn(nameof(LatipayRefundRecord.OrderId)).Ascending()
                .OnColumn(nameof(LatipayRefundRecord.RequestedOnUtc)).Descending();
        }

        const string refundPaymentAttemptIndexName = "IX_LatipayRefund_Attempt";
        if (!Schema.Table(refundRecordTable).Index(refundPaymentAttemptIndexName).Exists())
        {
            Create.Index(refundPaymentAttemptIndexName)
                .OnTable(refundRecordTable)
                .OnColumn(nameof(LatipayRefundRecord.PaymentAttemptId)).Ascending();
        }

        const string refundLatipayOrderIndexName = "IX_LatipayRefund_LatipayOrderId";
        if (!Schema.Table(refundRecordTable).Index(refundLatipayOrderIndexName).Exists())
        {
            Create.Index(refundLatipayOrderIndexName)
                .OnTable(refundRecordTable)
                .OnColumn(nameof(LatipayRefundRecord.LatipayOrderId)).Ascending();
        }
    }
}
