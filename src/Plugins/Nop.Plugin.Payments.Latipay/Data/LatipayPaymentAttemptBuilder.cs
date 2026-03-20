using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Payments.Latipay.Domain;

namespace Nop.Plugin.Payments.Latipay.Data;

/// <summary>
/// Maps the payment attempt entity.
/// </summary>
public class LatipayPaymentAttemptBuilder : NopEntityBuilder<LatipayPaymentAttempt>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(LatipayPaymentAttempt.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(LatipayPaymentAttempt.AttemptNumber)).AsInt32().NotNullable()
            .WithColumn(nameof(LatipayPaymentAttempt.MerchantReference)).AsString(100).NotNullable()
                .Unique("AK_LatipayPaymentAttempt_MerchantReference")
            .WithColumn(nameof(LatipayPaymentAttempt.SelectedSubPaymentMethod)).AsString(50).Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.LatipayOrderId)).AsString(100).Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.ExternalStatus)).AsString(100).Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.Amount)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(LatipayPaymentAttempt.Currency)).AsString(10).NotNullable()
            .WithColumn(nameof(LatipayPaymentAttempt.RedirectCreatedOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.CallbackReceivedOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.CallbackVerified)).AsBoolean().NotNullable()
            .WithColumn(nameof(LatipayPaymentAttempt.CallbackIdempotencyKey)).AsString(200).Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.PaymentCompletedOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.LastQueriedOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.RetryOfPaymentAttemptId)).AsInt32().Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.FailureReasonSummary)).AsString(1000).Nullable()
            .WithColumn(nameof(LatipayPaymentAttempt.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(LatipayPaymentAttempt.UpdatedOnUtc)).AsDateTime2().NotNullable();
    }
}
