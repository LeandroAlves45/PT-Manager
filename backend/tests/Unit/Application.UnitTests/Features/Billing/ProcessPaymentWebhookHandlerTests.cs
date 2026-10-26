using Application.Common.Observability;
using Application.Features.Billing;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Domain.ValueObjects;
using TestSupport.Observability;

namespace Application.UnitTests.Features.Billing;

public sealed class ProcessPaymentWebhookHandlerTests
{
    private static readonly DateTime Now = new(
        2026,
        9,
        1,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task UnknownEvent_SucceedsWithoutGatewayOrStoreEffects()
    {
        var gateway = new ReconciliationGatewayStub();
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(gateway, store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.Unknown, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task KnownEventWithExternalIdentity_RequestsCurrentSnapshotBeforeCommit()
    {
        var gateway = new ReconciliationGatewayStub { Snapshot = Snapshot() };
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(gateway, store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.SubscriptionUpdated, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal("cus_1", gateway.RequestedProviderCustomerId);
        Assert.Equal("sub_1", gateway.RequestedProviderSubscriptionId);
        Assert.Same(gateway.Snapshot, store.Snapshot);
    }

    [Fact]
    public async Task DuplicateEvent_SucceedsWithoutCallingProviderOrCommittingAgain()
    {
        var gateway = new ReconciliationGatewayStub { Snapshot = Snapshot() };
        var store = new PaymentEventStoreStub { IsProcessed = true };
        var handler = CreateHandler(gateway, store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.InvoicePaid, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData(SubscriptionReconciliationStatus.Disabled, "billing_provider_disabled")]
    [InlineData(SubscriptionReconciliationStatus.TransientFailure, "billing_provider_unavailable")]
    [InlineData(SubscriptionReconciliationStatus.NotFound, "billing_subscription_not_found")]
    [InlineData(SubscriptionReconciliationStatus.InvalidResponse, "billing_provider_invalid_response")]
    [InlineData(SubscriptionReconciliationStatus.ConfigurationMismatch, "billing_provider_configuration_mismatch")]
    public async Task ReconciliationFailure_MapsStableErrorWithoutLocalCommit(
        SubscriptionReconciliationStatus status,
        string expectedCode)
    {
        var gateway = new ReconciliationGatewayStub { Status = status };
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(gateway, store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.SubscriptionUpdated, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task KnownEventWithoutExternalIdentity_CommitsWithoutSnapshotRequest()
    {
        var gateway = new ReconciliationGatewayStub();
        var store = new PaymentEventStoreStub
        {
            Result = new(CommitPaymentEventStoreStatus.SubscriptionNotFound)
        };
        var handler = CreateHandler(gateway, store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.SubscriptionUpdated, null, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(1, store.Calls);
        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task KnownEvent_CommitsWithClockTimeAndOriginalEvent()
    {
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(new ReconciliationGatewayStub(), store);
        var paymentEvent = Event(PaymentEventKind.CheckoutCompleted, "cus_1", null);

        await handler.HandleAsync(paymentEvent, TestContext.Current.CancellationToken);

        Assert.Same(paymentEvent, store.PaymentEvent);
        Assert.Equal(Now, store.CommittedAt);
    }

    [Theory]
    [InlineData(CommitPaymentEventStoreStatus.Processed)]
    [InlineData(CommitPaymentEventStoreStatus.AlreadyProcessed)]
    public async Task CommitAcceptedOrDeduplicated_ReturnsSuccess(
        CommitPaymentEventStoreStatus status)
    {
        var store = new PaymentEventStoreStub { Result = new(status) };
        var handler = CreateHandler(new ReconciliationGatewayStub(), store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.InvoicePaid, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(
        CommitPaymentEventStoreStatus.SubscriptionNotFound,
        "billing_subscription_not_found")]
    [InlineData(
        CommitPaymentEventStoreStatus.ExternalIdentityConflict,
        "billing_external_identity_conflict")]
    [InlineData(
        CommitPaymentEventStoreStatus.ReconciliationRequired,
        "billing_reconciliation_required")]
    [InlineData(
        CommitPaymentEventStoreStatus.ConcurrencyConflict,
        "billing_concurrency_conflict")]
    public async Task CommitRejected_MapsStableError(
        CommitPaymentEventStoreStatus status,
        string expectedCode)
    {
        var store = new PaymentEventStoreStub { Result = new(status) };
        var handler = CreateHandler(new ReconciliationGatewayStub(), store);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.SubscriptionDeleted, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task InvoicePaid_PassesNetPaymentToTheSameCommit()
    {
        var gateway = new ReconciliationGatewayStub { Snapshot = Snapshot() };
        var payment = new InvoicePaymentSnapshot("in_1", 2900, 2789, "eur", Now.AddMinutes(-2));
        var invoices = new InvoicePaymentGatewayStub
        {
            Outcome = new(SubscriptionReconciliationStatus.Success, payment)
        };
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(gateway, store, invoices);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.InvoicePaid, "cus_1", "sub_1") with { ProviderInvoiceId = "in_1" },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("in_1", invoices.RequestedInvoiceId);
        Assert.Same(payment, store.InvoicePayment);
    }

    [Fact]
    public async Task InvoicePaid_WhenNetIsNotAvailableYet_FailsWithoutCommitting()
    {
        var invoices = new InvoicePaymentGatewayStub
        {
            Outcome = new(SubscriptionReconciliationStatus.TransientFailure)
        };
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(new ReconciliationGatewayStub { Snapshot = Snapshot() }, store, invoices);

        var result = await handler.HandleAsync(
            Event(PaymentEventKind.InvoicePaid, "cus_1", "sub_1") with { ProviderInvoiceId = "in_1" },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.ProviderUnavailable.Code, result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task OtherEvents_DoNotReadInvoicePayments()
    {
        var invoices = new InvoicePaymentGatewayStub();
        var store = new PaymentEventStoreStub();
        var handler = CreateHandler(new ReconciliationGatewayStub { Snapshot = Snapshot() }, store, invoices);

        await handler.HandleAsync(
            Event(PaymentEventKind.InvoicePaymentFailed, "cus_1", "sub_1") with { ProviderInvoiceId = "in_1" },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, invoices.Calls);
        Assert.Null(store.InvoicePayment);
    }

    [Theory]
    [InlineData("unknown", "ignored")]
    [InlineData("already_processed", "duplicate")]
    [InlineData("commit_processed", "processed")]
    [InlineData("commit_already_processed", "duplicate")]
    [InlineData("commit_rejected", "failed")]
    [InlineData("reconciliation_failed", "failed")]
    public async Task EachDelivery_IsCountedOnceByOutcome(string scenario, string expectedOutcome)
    {
        using var capture = new MetricCapture(PtManagerTelemetry.Name, "ptm.stripe.webhook");
        var gateway = new ReconciliationGatewayStub
        {
            Snapshot = Snapshot(),
            Status = scenario == "reconciliation_failed"
                ? SubscriptionReconciliationStatus.TransientFailure
                : SubscriptionReconciliationStatus.Success
        };
        var store = new PaymentEventStoreStub
        {
            IsProcessed = scenario == "already_processed",
            Result = new(scenario switch
            {
                "commit_already_processed" => CommitPaymentEventStoreStatus.AlreadyProcessed,
                "commit_rejected" => CommitPaymentEventStoreStatus.ConcurrencyConflict,
                _ => CommitPaymentEventStoreStatus.Processed
            })
        };
        var kind = scenario == "unknown" ? PaymentEventKind.Unknown : PaymentEventKind.SubscriptionUpdated;

        await CreateHandler(gateway, store).HandleAsync(
            Event(kind, "cus_1", "sub_1"),
            TestContext.Current.CancellationToken);

        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(1, measurement.Value);
        // Só o desfecho: nem o tipo nem o id do evento viram tag.
        var tag = Assert.Single(measurement.Tags);
        Assert.Equal("ptm.outcome", tag.Key);
        Assert.Equal(expectedOutcome, tag.Value);
    }

    private static ProcessPaymentWebhookHandler CreateHandler(
        ReconciliationGatewayStub gateway,
        PaymentEventStoreStub store,
        InvoicePaymentGatewayStub? invoices = null) =>
        new(gateway, invoices ?? new InvoicePaymentGatewayStub(), store, new BillingClock(Now));

    private static NormalizedPaymentEvent Event(
        PaymentEventKind kind,
        string? providerCustomerId,
        string? providerSubscriptionId) => new(
            "evt_test",
            "customer.subscription.updated",
            kind,
            providerCustomerId,
            providerSubscriptionId,
            "active",
            Guid.NewGuid(),
            Now.AddMinutes(-1));

    private static ProviderSubscriptionSnapshot Snapshot() => new(
        "cus_1",
        "sub_1",
        SubscriptionTier.Pro,
        "active",
        Now.AddDays(7),
        Now);
}
