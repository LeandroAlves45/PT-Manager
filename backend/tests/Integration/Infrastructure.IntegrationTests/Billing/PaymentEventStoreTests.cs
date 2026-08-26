using System.Text.Json;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Billing;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Billing;

[Collection(PostgresCollection.Name)]
public sealed class PaymentEventStoreTests(PostgresContainerFixture database)
{
    private static readonly DateTime Now = BillingTestSupport.Now;

    [Fact]
    public async Task Commit_AppliedFailureEvent_PersistsSubscriptionDeduplicationAndOutboxAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "commit-atomic",
            customerId: "cus_atomic",
            subscriptionId: "sub_atomic",
            cancellationToken: cancellationToken);
        var paymentEvent = FailureEvent("evt_atomic", "cus_atomic", "sub_atomic");
        await using var context = support.CreateRetryingWebhookContext(out var tenant);
        var store = new PaymentEventStore(context, tenant);

        var result = await store.CommitAsync(
            paymentEvent,
            Snapshot("cus_atomic", "sub_atomic", Now.AddMinutes(5)),
            Now.AddMinutes(6),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.Processed, result.Kind);
        Assert.Equal(1, tenant.EstablishCalls);
        await using var verification = database.CreateContext(trainer.TrainerId);
        var subscription = await verification.TrainerSubscriptions
            .SingleAsync(sub => sub.TrainerId == trainer.TrainerId, cancellationToken);
        Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(Now.AddMinutes(5), subscription.LastProviderStateObservedAt);
        Assert.True(await verification.ProcessedStripeEvents
            .AnyAsync(processed => processed.StripeEventId == "evt_atomic", cancellationToken));
        Assert.True(await verification.OutboxMessages
            .AnyAsync(message =>
                message.IdempotencyKey == "billing:evt_atomic:InvoicePaymentFailed" &&
                message.TrainerId == trainer.TrainerId &&
                message.MessageType == "billing_notification", cancellationToken));
    }

    [Fact]
    public async Task Commit_WhenOutboxInsertFails_RollsBackSubscriptionAndDeduplication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "commit-rollback",
            customerId: "cus_rollback",
            subscriptionId: "sub_rollback",
            cancellationToken: cancellationToken);
        await using (var seedContext = database.CreateContext(trainer.TrainerId))
        {
            seedContext.OutboxMessages.Add(new Domain.Entities.Jobs.OutboxMessage(
                trainer.TrainerId,
                "billing_notification",
                "{}",
                "billing:evt_rollback:InvoicePaymentFailed",
                Guid.NewGuid(),
                Now));
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = support.CreateRetryingWebhookContext(out var tenant);
        var store = new PaymentEventStore(context, tenant);

        await Assert.ThrowsAsync<DbUpdateException>(() => store.CommitAsync(
            FailureEvent("evt_rollback", "cus_rollback", "sub_rollback"),
            Snapshot("cus_rollback", "sub_rollback", Now.AddMinutes(5)),
            Now.AddMinutes(6),
            cancellationToken));

        await using var verification = database.CreateContext(trainer.TrainerId);
        var subscription = await verification.TrainerSubscriptions
            .SingleAsync(sub => sub.TrainerId == trainer.TrainerId, cancellationToken);
        Assert.Null(subscription.LastProviderStateObservedAt);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.False(await verification.ProcessedStripeEvents
            .AnyAsync(processed => processed.StripeEventId == "evt_rollback", cancellationToken));
    }

    [Fact]
    public async Task Commit_SequentialRedelivery_IsDeduplicatedWithoutAdditionalEffects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "redelivery-sequential",
            customerId: "cus_sequential",
            subscriptionId: "sub_sequential",
            cancellationToken: cancellationToken);
        var paymentEvent = FailureEvent("evt_sequential", "cus_sequential", "sub_sequential");
        var snapshot = Snapshot("cus_sequential", "sub_sequential", Now.AddMinutes(5));

        await using (var firstContext = support.CreateRetryingWebhookContext(out var firstTenant))
        {
            var firstResult = await new PaymentEventStore(firstContext, firstTenant).CommitAsync(
                paymentEvent,
                snapshot,
                Now.AddMinutes(6),
                cancellationToken);
            Assert.Equal(CommitPaymentEventStoreStatus.Processed, firstResult.Kind);
        }

        await using var secondContext = support.CreateRetryingWebhookContext(out var secondTenant);
        var secondResult = await new PaymentEventStore(secondContext, secondTenant).CommitAsync(
            paymentEvent,
            snapshot,
            Now.AddMinutes(7),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.AlreadyProcessed, secondResult.Kind);
        await using var verification = database.CreateContext(trainer.TrainerId);
        Assert.Equal(1, await verification.ProcessedStripeEvents
            .CountAsync(processed => processed.StripeEventId == "evt_sequential", cancellationToken));
        Assert.Equal(1, await verification.OutboxMessages
            .CountAsync(message =>
                message.IdempotencyKey == "billing:evt_sequential:InvoicePaymentFailed",
                cancellationToken));
    }

    [Fact]
    public async Task Commit_ConcurrentRedelivery_ProcessesExactlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "redelivery-concurrent",
            customerId: "cus_concurrent",
            subscriptionId: "sub_concurrent",
            cancellationToken: cancellationToken);
        var paymentEvent = FailureEvent("evt_concurrent", "cus_concurrent", "sub_concurrent");
        var snapshot = Snapshot("cus_concurrent", "sub_concurrent", Now.AddMinutes(5));
        await using var firstContext = support.CreateRetryingWebhookContext(out var firstTenant);
        await using var secondContext = support.CreateRetryingWebhookContext(out var secondTenant);

        var results = await Task.WhenAll(
            new PaymentEventStore(firstContext, firstTenant).CommitAsync(
                paymentEvent,
                snapshot,
                Now.AddMinutes(6),
                cancellationToken),
            new PaymentEventStore(secondContext, secondTenant).CommitAsync(
                paymentEvent,
                snapshot,
                Now.AddMinutes(6),
                cancellationToken));

        Assert.Single(results, result =>
            result.Kind == CommitPaymentEventStoreStatus.Processed);
        Assert.Single(results, result =>
            result.Kind == CommitPaymentEventStoreStatus.AlreadyProcessed);
        await using var verification = database.CreateContext(trainer.TrainerId);
        Assert.Equal(1, await verification.ProcessedStripeEvents
            .CountAsync(processed => processed.StripeEventId == "evt_concurrent", cancellationToken));
        Assert.Equal(1, await verification.OutboxMessages
            .CountAsync(message =>
                message.IdempotencyKey == "billing:evt_concurrent:InvoicePaymentFailed",
                cancellationToken));
    }

    [Fact]
    public async Task Commit_ExternalIdentitiesResolvingDifferentTrainers_ReturnsConflictWithoutEffects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        await support.SeedTrainerAsync(
            "crossed-customer",
            customerId: "cus_crossed_a",
            cancellationToken: cancellationToken);
        await support.SeedTrainerAsync(
            "crossed-subscription",
            customerId: "cus_crossed_b",
            subscriptionId: "sub_crossed_b",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingWebhookContext(out var tenant);
        var store = new PaymentEventStore(context, tenant);

        var result = await store.CommitAsync(
            new NormalizedPaymentEvent(
                "evt_crossed",
                "customer.subscription.updated",
                PaymentEventKind.SubscriptionUpdated,
                "cus_crossed_a",
                "sub_crossed_b",
                "active",
                Guid.NewGuid(),
                Now),
            null,
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.ExternalIdentityConflict, result.Kind);
        await using var verification = database.CreateAdministrativeContext();
        Assert.False(await verification.ProcessedStripeEvents
            .AnyAsync(processed => processed.StripeEventId == "evt_crossed", cancellationToken));
    }

    [Fact]
    public async Task Commit_StaleSnapshotAfterNewerSnapshot_IsDeduplicatedWithoutStateChangeOrOutbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "stale-snapshot",
            customerId: "cus_stale",
            subscriptionId: "sub_stale",
            cancellationToken: cancellationToken);
        await using (var newerContext = support.CreateRetryingWebhookContext(out var newerTenant))
        {
            var newerResult = await new PaymentEventStore(newerContext, newerTenant).CommitAsync(
                Event("evt_newer", PaymentEventKind.SubscriptionUpdated, "cus_stale", "sub_stale"),
                Snapshot("cus_stale", "sub_stale", Now.AddMinutes(10), "active", SubscriptionTier.Pro, 100),
                Now.AddMinutes(11),
                cancellationToken);
            Assert.Equal(CommitPaymentEventStoreStatus.Processed, newerResult.Kind);
        }

        await using var staleContext = support.CreateRetryingWebhookContext(out var staleTenant);
        var staleResult = await new PaymentEventStore(staleContext, staleTenant).CommitAsync(
            FailureEvent("evt_stale", "cus_stale", "sub_stale"),
            Snapshot("cus_stale", "sub_stale", Now.AddMinutes(5), "past_due", SubscriptionTier.Starter, 25),
            Now.AddMinutes(12),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.Processed, staleResult.Kind);
        await using var verification = database.CreateContext(trainer.TrainerId);
        var subscription = await verification.TrainerSubscriptions
            .SingleAsync(sub => sub.TrainerId == trainer.TrainerId, cancellationToken);
        Assert.Equal(SubscriptionTier.Pro, subscription.Tier);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(Now.AddMinutes(10), subscription.LastProviderStateObservedAt);
        Assert.True(await verification.ProcessedStripeEvents
            .AnyAsync(processed => processed.StripeEventId == "evt_stale", cancellationToken));
        Assert.False(await verification.OutboxMessages
            .AnyAsync(message =>
                message.IdempotencyKey == "billing:evt_stale:InvoicePaymentFailed",
                cancellationToken));
    }

    [Fact]
    public async Task Commit_NotificationPayload_UsesExactSnakeCaseProperties()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "payload-shape",
            customerId: "cus_payload",
            subscriptionId: "sub_payload",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingWebhookContext(out var tenant);

        var result = await new PaymentEventStore(context, tenant).CommitAsync(
            FailureEvent("evt_payload", "cus_payload", "sub_payload"),
            Snapshot("cus_payload", "sub_payload", Now.AddMinutes(5)),
            Now.AddMinutes(6),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.Processed, result.Kind);
        await using var verification = database.CreateContext(trainer.TrainerId);
        var payload = await verification.OutboxMessages
            .Where(message =>
                message.IdempotencyKey == "billing:evt_payload:InvoicePaymentFailed")
            .Select(message => message.Payload)
            .SingleAsync(cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var properties = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            // PostgreSQL jsonb não preserva a ordem textual das propriedades.
            // O contrato exige o conjunto exato de nomes, independentemente da ordem.
            .Order()
            .ToArray();
        Assert.Equal(
            ["event_id", "kind", "recipient_email", "trainer_id"],
            properties);
        Assert.Equal(
            trainer.TrainerId,
            document.RootElement.GetProperty("trainer_id").GetGuid());
        Assert.Equal(
            trainer.Email,
            document.RootElement.GetProperty("recipient_email").GetString());
        Assert.Equal(
            "evt_payload",
            document.RootElement.GetProperty("event_id").GetString());
        Assert.Equal(
            "InvoicePaymentFailed",
            document.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Commit_InactiveTrainer_ConfirmsStateAndDeduplicationWithoutNotification()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "inactive-trainer",
            customerId: "cus_inactive",
            subscriptionId: "sub_inactive",
            isActive: false,
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingWebhookContext(out var tenant);

        var result = await new PaymentEventStore(context, tenant).CommitAsync(
            FailureEvent("evt_inactive", "cus_inactive", "sub_inactive"),
            Snapshot("cus_inactive", "sub_inactive", Now.AddMinutes(5)),
            Now.AddMinutes(6),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.Processed, result.Kind);
        await using var verification = database.CreateContext(trainer.TrainerId);
        var subscription = await verification.TrainerSubscriptions
            .SingleAsync(sub => sub.TrainerId == trainer.TrainerId, cancellationToken);
        Assert.Equal(Now.AddMinutes(5), subscription.LastProviderStateObservedAt);
        Assert.True(await verification.ProcessedStripeEvents
            .AnyAsync(processed => processed.StripeEventId == "evt_inactive", cancellationToken));
        Assert.False(await verification.OutboxMessages
            .AnyAsync(message =>
                message.IdempotencyKey == "billing:evt_inactive:InvoicePaymentFailed",
                cancellationToken));
    }

    [Fact]
    public async Task Commit_TrialWillEndWithoutSnapshot_IsDeduplicatedWithoutNotification()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "trial-no-snapshot",
            customerId: "cus_trial",
            subscriptionId: "sub_trial",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingWebhookContext(out var tenant);

        var result = await new PaymentEventStore(context, tenant).CommitAsync(
            Event("evt_trial", PaymentEventKind.TrialWillEnd, "cus_trial", "sub_trial"),
            null,
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(CommitPaymentEventStoreStatus.Processed, result.Kind);
        await using var verification = database.CreateContext(trainer.TrainerId);
        var subscription = await verification.TrainerSubscriptions
            .SingleAsync(sub => sub.TrainerId == trainer.TrainerId, cancellationToken);
        Assert.Null(subscription.LastProviderStateObservedAt);
        Assert.True(await verification.ProcessedStripeEvents
            .AnyAsync(processed => processed.StripeEventId == "evt_trial", cancellationToken));
        Assert.False(await verification.OutboxMessages
            .AnyAsync(message =>
                message.IdempotencyKey == "billing:evt_trial:TrialWillEnd",
                cancellationToken));
    }

    private static NormalizedPaymentEvent FailureEvent(
        string eventId,
        string customerId,
        string subscriptionId) => Event(
            eventId,
            PaymentEventKind.InvoicePaymentFailed,
            customerId,
            subscriptionId);

    private static NormalizedPaymentEvent Event(
        string eventId,
        PaymentEventKind kind,
        string customerId,
        string subscriptionId) => new(
            eventId,
            "billing.integration.test",
            kind,
            customerId,
            subscriptionId,
            "past_due",
            Guid.NewGuid(),
            Now);

    private static ProviderSubscriptionSnapshot Snapshot(
        string customerId,
        string subscriptionId,
        DateTime observedAt,
        string providerStatus = "past_due",
        SubscriptionTier? tier = null,
        int clientLimit = 25) => new(
            customerId,
            subscriptionId,
            tier ?? SubscriptionTier.Starter,
            clientLimit,
            providerStatus,
            null,
            observedAt);
}
