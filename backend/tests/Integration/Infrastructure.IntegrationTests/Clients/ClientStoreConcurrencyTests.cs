using Application.Features.Clients.Abstractions;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Clients;

/// <summary>Verifica as invariantes transacionais com workers independentes.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ClientStoreConcurrencyTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly ClientStoreTestContextFactory _contextFactory;

    public ClientStoreConcurrencyTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _contextFactory = new ClientStoreTestContextFactory(fixture.ConnectionString);
    }

    public static IEnumerable<object[]> CapacityCases
    {
        get
        {
            yield return ["ACTIVE", false, 4, 5, true];
            yield return ["ACTIVE", false, 5, 5, false];
            yield return ["INACTIVE", false, 0, 5, false];
            yield return ["SUSPENDED", false, 0, 5, false];
            yield return ["CANCELLED", false, 0, 5, false];
            yield return ["ACTIVE", true, 5, 5, true];
            yield return ["CANCELLED", true, 5, 5, true];
        }
    }

    [Fact]
    public async Task LastSubscriptionSlot_TwoWorkers_OnlyOneCreates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 4);
        var clientA = ClientPersistenceTestData.CreateClient(trainer.Id, $"a-{discriminator}");
        var clientB = ClientPersistenceTestData.CreateClient(trainer.Id, $"b-{discriminator}");
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription);
        using var barrier = new Barrier(participantCount: 3);

        var workerA = Task.Run(async () =>
        {
            await using var context = _contextFactory.Create(trainer.Id);
            barrier.SignalAndWait(cancellationToken);
            return await CreateStore(context).CreateWithSubscriptionSlotAsync(
                clientA,
                trainer.Id,
                ClientPersistenceTestData.NowUtc.AddHours(1),
                cancellationToken);
        }, cancellationToken);
        var workerB = Task.Run(async () =>
        {
            await using var context = _contextFactory.Create(trainer.Id);
            barrier.SignalAndWait(cancellationToken);
            return await CreateStore(context).CreateWithSubscriptionSlotAsync(
                clientB,
                trainer.Id,
                ClientPersistenceTestData.NowUtc.AddHours(1),
                cancellationToken);
        }, cancellationToken);
        barrier.SignalAndWait(cancellationToken);

        var outcomes = await Task.WhenAll(workerA, workerB);

        await using var readContext = _fixture.CreateContext(trainer.Id);
        var persistedIds = await readContext.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientA.Id || client.Id == clientB.Id)
            .Select(client => client.Id)
            .ToListAsync(cancellationToken);
        var finalCount = await readContext.TrainerSubscriptions
            .AsNoTracking()
            .Where(item => item.TrainerId == trainer.Id)
            .Select(item => item.CurrentClientCount)
            .SingleAsync(cancellationToken);
        Assert.Equal(1, outcomes.Count(outcome => outcome == CreateClientStoreOutcome.Created));
        Assert.Equal(1, outcomes.Count(outcome => outcome == CreateClientStoreOutcome.ClientLimitReached));
        Assert.Equal(5, finalCount);
        Assert.Single(persistedIds);
    }

    [Fact]
    public async Task Archive_TwoWorkers_DecrementsExactlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedClientAsync(isActive: true, currentClientCount: 1);
        using var barrier = new Barrier(participantCount: 3);

        var workerA = RunArchiveWorkerAsync(seed, barrier, cancellationToken);
        var workerB = RunArchiveWorkerAsync(seed, barrier, cancellationToken);
        barrier.SignalAndWait(cancellationToken);
        var outcomes = await Task.WhenAll(workerA, workerB);

        var state = await ReadStateAsync(seed, cancellationToken);
        Assert.Contains(ArchiveClientStoreOutcome.Archived, outcomes);
        Assert.Contains(ArchiveClientStoreOutcome.AlreadyArchived, outcomes);
        Assert.False(state.IsActive);
        Assert.Equal(0, state.CurrentClientCount);
    }

    [Fact]
    public async Task Reactivate_TwoWorkers_IncrementsExactlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedClientAsync(isActive: false, currentClientCount: 0);
        using var barrier = new Barrier(participantCount: 3);

        var workerA = RunReactivateWorkerAsync(seed, barrier, cancellationToken);
        var workerB = RunReactivateWorkerAsync(seed, barrier, cancellationToken);
        barrier.SignalAndWait(cancellationToken);
        var outcomes = await Task.WhenAll(workerA, workerB);

        var state = await ReadStateAsync(seed, cancellationToken);
        Assert.Contains(ReactivateClientStoreOutcome.Reactivated, outcomes);
        Assert.Contains(ReactivateClientStoreOutcome.AlreadyActive, outcomes);
        Assert.True(state.IsActive);
        Assert.Equal(1, state.CurrentClientCount);
    }

    [Theory]
    [MemberData(nameof(CapacityCases))]
    public async Task AtomicCapacityPredicate_MatchesCanAddClient(
        string statusValue,
        bool isExempt,
        int currentCount,
        int clientLimit,
        bool expectedCanAdd)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.FromString(statusValue),
            clientLimit,
            currentCount,
            isExempt);
        var client = ClientPersistenceTestData.CreateClient(trainer.Id, discriminator);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription);
        await using var context = _contextFactory.Create(trainer.Id);

        var outcome = await CreateStore(context).CreateWithSubscriptionSlotAsync(
            client,
            trainer.Id,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            cancellationToken);

        await using var readContext = _fixture.CreateContext(trainer.Id);
        var clientExists = await readContext.Clients
            .AsNoTracking()
            .AnyAsync(item => item.Id == client.Id, cancellationToken);
        var finalCount = await readContext.TrainerSubscriptions
            .AsNoTracking()
            .Where(item => item.TrainerId == trainer.Id)
            .Select(item => item.CurrentClientCount)
            .SingleAsync(cancellationToken);
        Assert.Equal(expectedCanAdd, subscription.CanAddClient());
        Assert.Equal(expectedCanAdd, outcome == CreateClientStoreOutcome.Created);
        Assert.Equal(expectedCanAdd, clientExists);
        Assert.Equal(currentCount + (expectedCanAdd ? 1 : 0), finalCount);
    }

    private Task<ArchiveClientStoreOutcome> RunArchiveWorkerAsync(
        PersistedClientSeed seed,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await using var context = _contextFactory.Create(seed.TrainerId);
            barrier.SignalAndWait(cancellationToken);
            return await CreateStore(context).ArchiveAsync(
                seed.ClientId,
                seed.TrainerId,
                ClientPersistenceTestData.NowUtc.AddHours(1),
                cancellationToken);
        }, cancellationToken);
    }

    private Task<ReactivateClientStoreOutcome> RunReactivateWorkerAsync(
        PersistedClientSeed seed,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await using var context = _contextFactory.Create(seed.TrainerId);
            barrier.SignalAndWait(cancellationToken);
            return await CreateStore(context).ReactivateAsync(
                seed.ClientId,
                seed.TrainerId,
                ClientPersistenceTestData.NowUtc.AddHours(1),
                cancellationToken);
        }, cancellationToken);
    }

    private async Task<PersistedClientSeed> SeedClientAsync(
        bool isActive,
        int currentClientCount)
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.Active,
            5,
            currentClientCount);
        var client = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            discriminator,
            isActive);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription,
            client);
        return new PersistedClientSeed(trainer.Id, client.Id);
    }

    private async Task<ClientState> ReadStateAsync(
        PersistedClientSeed seed,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var isActive = await context.Clients
            .AsNoTracking()
            .Where(client => client.Id == seed.ClientId)
            .Select(client => client.IsActive)
            .SingleAsync(cancellationToken);
        var currentClientCount = await context.TrainerSubscriptions
            .AsNoTracking()
            .Where(item => item.TrainerId == seed.TrainerId)
            .Select(item => item.CurrentClientCount)
            .SingleAsync(cancellationToken);
        return new ClientState(isActive, currentClientCount);
    }

    private static ClientStore CreateStore(Infrastructure.Data.PtManagerDbContext context)
    {
        return new ClientStore(context, new PostgresConstraintTranslator());
    }

    private static string NewDiscriminator() => Guid.NewGuid().ToString("N");

    private sealed record PersistedClientSeed(Guid TrainerId, Guid ClientId);

    private sealed record ClientState(bool IsActive, int CurrentClientCount);
}
