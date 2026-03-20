# Latipay Manual QA Checklist

Use this checklist against a test store with valid Latipay credentials and externally reachable callback/return endpoints.

## Configuration

- [ ] Plugin installs successfully and configuration page loads without exposing the stored API key or hosted-card private key.
- [ ] Saving configuration fails with clear validation when required credentials are missing.
- [ ] Saving configuration fails with clear validation when hosted-card credentials are missing but the hosted-card method is enabled.
- [ ] Saving configuration fails when no sub-payment methods are enabled while the plugin is enabled.
- [ ] Latipay is hidden from checkout when the working currency is not `NZD`.
- [ ] Latipay appears in checkout when the plugin is enabled, credentials are valid, and at least one sub-payment method is enabled.
- [ ] Hosted card appears as a single `Card (Visa/Mastercard)` option rather than separate brand-specific choices.

## Checkout And Payment

- [ ] Successful payment:
  Expected: customer is redirected to Latipay, callback or reconciliation confirms payment, order becomes paid once, and a success note is added.
- [ ] Successful hosted card payment:
  Expected: customer is redirected to the Latipay hosted card page, callback or reconciliation confirms payment, and the captured provider transaction ID is stored for later refunds.
- [ ] Cancelled payment:
  Expected: order stays pending, retry remains available when safe, and order notes explain the verified non-paid status.
- [ ] Abandoned payment:
  Expected: order stays pending, no false paid state is applied, and reconciliation can continue later.
- [ ] Callback before return:
  Expected: callback marks the order paid after verification; later browser return does not duplicate notes or state changes.
- [ ] Return before callback:
  Expected: browser return does not mark the order paid by itself; reconciliation may confirm payment, otherwise the order remains pending until callback or later reconciliation.
- [ ] Duplicate callback:
  Expected: duplicate provider callback is acknowledged safely without double-processing, duplicate notes, or repeated paid transitions.
- [ ] Invalid signature handling:
  Expected: callback/return with an invalid signature does not mark the order paid and leaves an audit trail for review.
- [ ] Missing hosted-card billing details:
  Expected: the hosted card attempt is not started, the order remains pending, and the customer/admin get a safe retryable failure path.
- [ ] Non-NZD rejection:
  Expected: Latipay cannot be selected or submitted when the customer currency is not `NZD`.

## Retry

- [ ] Retry payment:
  Expected: the order details page offers retry only for unpaid Latipay orders in a safe retryable state.
- [ ] Reselection of sub-method on retry:
  Expected: the retry page shows only enabled methods and allows choosing a different enabled method.
- [ ] Retry uses the same order:
  Expected: no new nopCommerce order is created; a new `LatipayPaymentAttempt` is linked to the existing order.
- [ ] Duplicate rapid retry submission:
  Expected: concurrent retry submissions do not create multiple active attempts from the same click burst.
- [ ] Retry blocked after payment confirmed:
  Expected: retry is denied once the order is already paid, fully refunded, cancelled, or otherwise not safely payable.

## Refunds

- [ ] Full refund:
  Expected: successful full refund updates the order payment status to refunded and stores a successful refund record.
- [ ] Partial refund:
  Expected: successful partial refund updates the order payment status to partially refunded and stores a successful refund record.
- [ ] Refund over-limit rejection:
  Expected: refund is blocked when the requested amount exceeds the remaining refundable balance.
- [ ] Duplicate refund protection:
  Expected: duplicate admin refund submissions do not create duplicate confirmed refunds.
- [ ] Ambiguous refund response:
  Expected: refund remains review-required, the order is not overstated as refunded, and further refunds are blocked until review.

## Reconciliation

- [ ] Reconciliation of pending payment:
  Expected: manual or scheduled reconciliation queries Latipay by `merchant_reference` and applies only verified legal state transitions.
- [ ] Manual reconcile action:
  Expected: admin can run a one-off reconcile by `merchant_reference` and receives a success, pending, or review-required message.
- [ ] Stale or conflicting external status:
  Expected: the order remains pending or review-required rather than being forced into an unsafe state.

## Data And Audit

- [ ] Payment attempts:
  Expected: each hosted start and retry creates a distinct `LatipayPaymentAttempt` with a unique `merchant_reference`.
- [ ] Refund history:
  Expected: each refund attempt creates or updates a `LatipayRefundRecord` with the correct local status.
- [ ] Log redaction:
  Expected: logs do not expose API keys or other secrets, even when debug logging is enabled.
- [ ] Order notes:
  Expected: notes exist for key payment, callback, retry, reconcile, and refund events without duplicate noise on idempotent flows.
