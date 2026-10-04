using Application.Features.Billing.Abstractions;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Billing;

namespace Infrastructure.IntegrationTests.Billing;

[Collection(PostgresCollection.Name)]
public sealed class BillingCheckoutStoreTests(PostgresContainerFixture database)
{
    [Fact]
    public async Task Reserve_WhenRequestedForAnotherTenant_DoesNotExposeSubscription()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var owner = await support.SeedTrainerAsync("checkout-owner", cancellationToken: token);
        var other = await support.SeedTrainerAsync("checkout-other", cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(owner.TrainerId);

        var result = await new BillingCheckoutStore(context).ReserveAsync(
            other.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Pro,
            BillingTestSupport.Now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.SubscriptionNotFound, result.Status);
    }

    [Fact]
    public async Task Reserve_SameKeyAfterLeaseExpires_ResumesSameOperationAndTrial()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-resume", cancellationToken: token);
        var operationKey = Guid.NewGuid();
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);

        var first = await store.ReserveAsync(
            trainer.TrainerId,
            operationKey,
            SubscriptionTier.Starter,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);
        var resumed = await store.ReserveAsync(
            trainer.TrainerId,
            operationKey,
            SubscriptionTier.Starter,
            BillingTestSupport.Now.AddMinutes(3),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.Acquired, first.Status);
        Assert.Equal(CheckoutReservationStatus.Acquired, resumed.Status);
        Assert.Equal(first.OperationId, resumed.OperationId);
        Assert.Equal(first.EffectiveTrialEndsAt, resumed.EffectiveTrialEndsAt);
        Assert.NotEqual(first.LeaseOwnerId, resumed.LeaseOwnerId);
    }

    [Fact]
    public async Task Reserve_ConcurrentDifferentKeys_AllowsOnlyOneActiveOperation()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-concurrent", cancellationToken: token);
        await using var contextA = support.CreateRetryingTrainerContext(trainer.TrainerId);
        await using var contextB = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var now = BillingTestSupport.Now.AddMinutes(1);

        var results = await Task.WhenAll(
            new BillingCheckoutStore(contextA).ReserveAsync(
                trainer.TrainerId, Guid.NewGuid(), SubscriptionTier.Starter,
                now, TimeSpan.FromMinutes(2), token),
            new BillingCheckoutStore(contextB).ReserveAsync(
                trainer.TrainerId, Guid.NewGuid(), SubscriptionTier.Pro,
                now, TimeSpan.FromMinutes(2), token));

        Assert.Single(results, result => result.Status == CheckoutReservationStatus.Acquired);
        Assert.Single(results, result => result.Status == CheckoutReservationStatus.AnotherOperationActive);
    }

    [Fact]
    public async Task LinkCustomer_ForReservedOperation_PersistsAssociation()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-link", cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);
        var reserved = await store.ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Pro,
            BillingTestSupport.Now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            token);

        var status = await store.LinkCustomerAsync(
            reserved.OperationId,
            reserved.LeaseOwnerId,
            " cus_retry_strategy ",
            BillingTestSupport.Now.AddMinutes(1),
            token);

        Assert.Equal(CheckoutMutationStatus.Applied, status);
        Assert.Equal("cus_retry_strategy", await store.GetCustomerIdAsync(trainer.TrainerId, token));
    }

    [Fact]
    public async Task Reserve_SameKeyWhileLeaseActive_ResumesSameOwner()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-same-lease", cancellationToken: token);
        var operationKey = Guid.NewGuid();
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);

        var first = await store.ReserveAsync(
            trainer.TrainerId,
            operationKey,
            SubscriptionTier.Starter,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);
        var retry = await store.ReserveAsync(
            trainer.TrainerId,
            operationKey,
            SubscriptionTier.Starter,
            BillingTestSupport.Now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.Acquired, first.Status);
        Assert.Equal(CheckoutReservationStatus.Acquired, retry.Status);
        Assert.Equal(first.OperationId, retry.OperationId);
        Assert.Equal(first.LeaseOwnerId, retry.LeaseOwnerId);
    }

    [Fact]
    public async Task Reserve_SameKeyAfterFailed_ReopensSameOperation()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-failed-retry", cancellationToken: token);
        var operationKey = Guid.NewGuid();
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);
        var first = await store.ReserveAsync(
            trainer.TrainerId,
            operationKey,
            SubscriptionTier.Pro,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);

        await store.MarkFailedAsync(
            first.OperationId,
            first.LeaseOwnerId,
            "transient_failure",
            BillingTestSupport.Now.AddMinutes(1),
            token);
        var retry = await store.ReserveAsync(
            trainer.TrainerId,
            operationKey,
            SubscriptionTier.Pro,
            BillingTestSupport.Now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.Acquired, retry.Status);
        Assert.Equal(first.OperationId, retry.OperationId);
        Assert.NotEqual(first.LeaseOwnerId, retry.LeaseOwnerId);
    }

    [Fact]
    public async Task Reserve_NewKeyForSameTierWithOpenSession_ResumesThatSession()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-new-key-same-tier", cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);
        var opened = await OpenSessionAsync(store, trainer.TrainerId, SubscriptionTier.Starter, token);

        var resumed = await store.ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Starter,
            BillingTestSupport.Now.AddMinutes(5),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.ResumeCreated, resumed.Status);
        Assert.Equal(opened.OperationId, resumed.OperationId);
        Assert.Equal("cs_test_open_session", resumed.ProviderSessionId);
    }

    [Fact]
    public async Task Reserve_NewKeyForOtherTierWithOpenSession_ReturnsAnotherOperationActive()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-new-key-other-tier", cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);
        await OpenSessionAsync(store, trainer.TrainerId, SubscriptionTier.Starter, token);

        var other = await store.ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Pro,
            BillingTestSupport.Now.AddMinutes(5),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.AnotherOperationActive, other.Status);
    }

    [Fact]
    public async Task Reserve_NewKeyForSameTierWhileOtherKeyPending_ReturnsAnotherOperationActive()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-new-key-pending", cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);
        var pending = await store.ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Starter,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);
        Assert.Equal(CheckoutReservationStatus.Acquired, pending.Status);

        // Lease ainda ativo e sem sessão Stripe: não há nada para retomar.
        var other = await store.ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Starter,
            BillingTestSupport.Now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.AnotherOperationActive, other.Status);
    }

    [Fact]
    public async Task Reserve_NewKeyForSameTierAfterSessionExpired_StartsNewOperation()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync("checkout-new-key-expired", cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);
        var opened = await OpenSessionAsync(store, trainer.TrainerId, SubscriptionTier.Starter, token);

        var fresh = await store.ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Starter,
            BillingTestSupport.Now.AddMinutes(31),
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.Acquired, fresh.Status);
        Assert.NotEqual(opened.OperationId, fresh.OperationId);
    }

    /// <summary>Reserva e marca a sessão Stripe como criada, válida durante 30 minutos.</summary>
    private static async Task<CheckoutReservationResult> OpenSessionAsync(
        BillingCheckoutStore store,
        Guid trainerId,
        SubscriptionTier tier,
        CancellationToken token)
    {
        var reserved = await store.ReserveAsync(
            trainerId,
            Guid.NewGuid(),
            tier,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);
        Assert.Equal(CheckoutReservationStatus.Acquired, reserved.Status);

        var created = await store.MarkSessionCreatedAsync(
            reserved.OperationId,
            reserved.LeaseOwnerId,
            "cs_test_open_session",
            BillingTestSupport.Now.AddMinutes(30),
            BillingTestSupport.Now.AddMinutes(1),
            token);
        Assert.Equal(CheckoutMutationStatus.Applied, created);

        return reserved;
    }
}
