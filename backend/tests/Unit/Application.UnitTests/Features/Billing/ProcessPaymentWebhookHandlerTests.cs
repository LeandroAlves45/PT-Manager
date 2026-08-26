using Application.Features.Billing;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Domain.ValueObjects;

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
            Event(PaymentEventKind.InvoicePaymentSucceeded, "cus_1", "sub_1"),
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

    private static ProcessPaymentWebhookHandler CreateHandler(
        ReconciliationGatewayStub gateway,
        PaymentEventStoreStub store) => new(gateway, store, new BillingClock(Now));

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
        100,
        "active",
        Now.AddDays(7),
        Now);
}
