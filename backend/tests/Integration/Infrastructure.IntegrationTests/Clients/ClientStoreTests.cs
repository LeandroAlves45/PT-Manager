using Application.Features.Clients.Abstractions;
using Domain.Entities.Clients;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Clients;

/// <summary>Verifica constraints, rollback, transições e isolamento do store.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ClientStoreTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly ClientStoreTestContextFactory _contextFactory;

    public ClientStoreTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _contextFactory = new ClientStoreTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Create_WithCapacity_CommitsClientAndCounter()
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 4);
        var client = ClientPersistenceTestData.CreateClient(trainer.Id, discriminator);
        var now = ClientPersistenceTestData.NowUtc.AddHours(1);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription);
        await using var context = _contextFactory.Create(trainer.Id);
        var store = CreateStore(context);

        var outcome = await store.CreateWithSubscriptionSlotAsync(
            client,
            trainer.Id,
            now,
            CancellationToken.None);

        var state = await ReadStateAsync(trainer.Id, client.Id);
        Assert.Equal(CreateClientStoreOutcome.Created, outcome);
        Assert.True(state.ClientExists);
        Assert.Equal(5, state.CurrentClientCount);
        Assert.Equal(now, state.SubscriptionUpdatedAt);
    }

    [Fact]
    public async Task Create_AtLimit_RollsBackInsert()
    {
        var seed = await SeedCreateAsync(clientLimit: 5, currentClientCount: 5);
        var client = ClientPersistenceTestData.CreateClient(seed.TrainerId, NewDiscriminator());
        await using var context = _contextFactory.Create(seed.TrainerId);
        var store = CreateStore(context);

        var outcome = await store.CreateWithSubscriptionSlotAsync(
            client,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, client.Id);
        Assert.Equal(CreateClientStoreOutcome.ClientLimitReached, outcome);
        Assert.False(state.ClientExists);
        Assert.Equal(5, state.CurrentClientCount);
    }

    [Fact]
    public async Task Create_DuplicateEmail_TranslatesConstraintAndPreservesCounter()
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 1);
        var existingClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"existing-{discriminator}");
        var duplicateClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"duplicate-{discriminator}",
            contactEmail: existingClient.ContactEmail,
            phone: "+351900000001");
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription,
            existingClient);
        await using var context = _contextFactory.Create(trainer.Id);
        var store = CreateStore(context);

        var outcome = await store.CreateWithSubscriptionSlotAsync(
            duplicateClient,
            trainer.Id,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(trainer.Id, duplicateClient.Id);
        Assert.Equal(CreateClientStoreOutcome.DuplicateEmail, outcome);
        Assert.False(state.ClientExists);
        Assert.Equal(1, state.CurrentClientCount);
    }

    [Fact]
    public async Task Create_DuplicatePhone_TranslatesConstraintAndPreservesCounter()
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 1);
        var existingClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"existing-{discriminator}");
        var duplicateClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"duplicate-{discriminator}",
            contactEmail: $"different-{discriminator}@example.test",
            phone: existingClient.Phone);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription,
            existingClient);
        await using var context = _contextFactory.Create(trainer.Id);
        var store = CreateStore(context);

        var outcome = await store.CreateWithSubscriptionSlotAsync(
            duplicateClient,
            trainer.Id,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(trainer.Id, duplicateClient.Id);
        Assert.Equal(CreateClientStoreOutcome.DuplicatePhone, outcome);
        Assert.False(state.ClientExists);
        Assert.Equal(1, state.CurrentClientCount);
    }

    [Theory]
    [InlineData("INACTIVE", CreateClientStoreOutcome.SubscriptionInactive)]
    [InlineData("SUSPENDED", CreateClientStoreOutcome.SubscriptionSuspended)]
    [InlineData("CANCELLED", CreateClientStoreOutcome.SubscriptionCancelled)]
    public async Task Create_BlockedStatus_ReturnsExactOutcomeAndRollsBack(
        string statusValue,
        CreateClientStoreOutcome expectedOutcome)
    {
        var status = SubscriptionStatus.FromString(statusValue);
        var seed = await SeedCreateAsync(status: status);
        var client = ClientPersistenceTestData.CreateClient(seed.TrainerId, NewDiscriminator());
        await using var context = _contextFactory.Create(seed.TrainerId);
        var store = CreateStore(context);

        var outcome = await store.CreateWithSubscriptionSlotAsync(
            client,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, client.Id);
        Assert.Equal(expectedOutcome, outcome);
        Assert.False(state.ClientExists);
        Assert.Equal(0, state.CurrentClientCount);
    }

    [Fact]
    public async Task Create_MissingSubscription_RollsBackInsert()
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var client = ClientPersistenceTestData.CreateClient(trainer.Id, discriminator);
        await ClientPersistenceTestData.PersistAsync(_fixture, trainer.Id, trainer);
        await using var context = _contextFactory.Create(trainer.Id);
        var store = CreateStore(context);

        var outcome = await store.CreateWithSubscriptionSlotAsync(
            client,
            trainer.Id,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(trainer.Id, client.Id);
        Assert.Equal(CreateClientStoreOutcome.SubscriptionMissing, outcome);
        Assert.False(state.ClientExists);
        Assert.Null(state.CurrentClientCount);
    }

    [Fact]
    public async Task Create_SameEmailAcrossTenants_IsAllowed()
    {
        const string sharedEmail = "shared-client@example.test";
        var discriminator = NewDiscriminator();
        var trainerA = ClientPersistenceTestData.CreateTrainer($"a-{discriminator}");
        var trainerB = ClientPersistenceTestData.CreateTrainer($"b-{discriminator}");
        var subscriptionA = ClientPersistenceTestData.CreateSubscription(
            trainerA.Id,
            SubscriptionStatus.Active,
            5,
            0);
        var subscriptionB = ClientPersistenceTestData.CreateSubscription(
            trainerB.Id,
            SubscriptionStatus.Active,
            5,
            0);
        var clientA = ClientPersistenceTestData.CreateClient(
            trainerA.Id,
            $"a-{discriminator}",
            contactEmail: sharedEmail);
        var clientB = ClientPersistenceTestData.CreateClient(
            trainerB.Id,
            $"b-{discriminator}",
            contactEmail: sharedEmail);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainerA.Id,
            trainerA,
            subscriptionA);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainerB.Id,
            trainerB,
            subscriptionB);
        await using var contextA = _contextFactory.Create(trainerA.Id);
        await using var contextB = _contextFactory.Create(trainerB.Id);

        var outcomeA = await CreateStore(contextA).CreateWithSubscriptionSlotAsync(
            clientA,
            trainerA.Id,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);
        var outcomeB = await CreateStore(contextB).CreateWithSubscriptionSlotAsync(
            clientB,
            trainerB.Id,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        Assert.Equal(CreateClientStoreOutcome.Created, outcomeA);
        Assert.Equal(CreateClientStoreOutcome.Created, outcomeB);
        Assert.True((await ReadStateAsync(trainerA.Id, clientA.Id)).ClientExists);
        Assert.True((await ReadStateAsync(trainerB.Id, clientB.Id)).ClientExists);
    }

    [Fact]
    public async Task SaveProfile_WithUniqueValues_CommitsTrackedChanges()
    {
        var seed = await SeedPersistedClientAsync(isActive: true, withSubscription: true);
        var now = ClientPersistenceTestData.NowUtc.AddHours(2);
        await using var context = _contextFactory.Create(seed.TrainerId);
        var store = CreateStore(context);
        var client = await store.GetForUpdateAsync(seed.ClientId, CancellationToken.None);
        Assert.NotNull(client);
        client.UpdateProfile(
            "Updated Name",
            "updated-profile@example.test",
            "+351900000010",
            BirthDate.Create(new DateOnly(1994, 2, 2), DateOnly.FromDateTime(now)),
            BiologicalSex.Male,
            "Hypertrophy",
            "Updated notes",
            "Emergency Name",
            "+351900000011",
            now);

        var outcome = await store.SaveProfileAsync(client, CancellationToken.None);

        await using var readContext = _fixture.CreateContext(seed.TrainerId);
        var persisted = await readContext.Clients.SingleAsync(
            item => item.Id == seed.ClientId,
            TestContext.Current.CancellationToken);
        Assert.Equal(SaveClientProfileOutcome.Updated, outcome);
        Assert.Equal("Updated Name", persisted.Name);
        Assert.Equal("updated-profile@example.test", persisted.ContactEmail);
        Assert.Equal("+351900000010", persisted.Phone);
        Assert.Equal(now, persisted.UpdatedAt);
    }

    [Fact]
    public async Task SaveProfile_DuplicateEmail_ReturnsExactOutcome()
    {
        await AssertDuplicateProfileAsync(
            SaveClientProfileOutcome.DuplicateEmail,
            (first, second, now) => second.UpdateProfile(
                second.Name,
                first.ContactEmail,
                second.Phone,
                second.BirthDate,
                second.Sex,
                second.Objective,
                second.Notes,
                second.EmergencyContactName,
                second.EmergencyContactPhone,
                now));
    }

    [Fact]
    public async Task SaveProfile_DuplicatePhone_ReturnsExactOutcome()
    {
        await AssertDuplicateProfileAsync(
            SaveClientProfileOutcome.DuplicatePhone,
            (first, second, now) => second.UpdateProfile(
                second.Name,
                second.ContactEmail,
                first.Phone,
                second.BirthDate,
                second.Sex,
                second.Objective,
                second.Notes,
                second.EmergencyContactName,
                second.EmergencyContactPhone,
                now));
    }

    [Fact]
    public async Task Archive_ActiveClient_MatchesDomainTransition()
    {
        var seed = await SeedPersistedClientAsync(isActive: true, withSubscription: true);
        var control = ClientPersistenceTestData.CreateClient(Guid.NewGuid(), NewDiscriminator());
        var now = ClientPersistenceTestData.NowUtc.AddHours(1);
        control.Deactivate(now);
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ArchiveAsync(
            seed.ClientId,
            seed.TrainerId,
            now,
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(ArchiveClientStoreOutcome.Archived, outcome);
        Assert.Equal(control.IsActive, state.IsActive);
        Assert.Equal(control.IsDeleted, state.IsDeleted);
        Assert.Equal(control.UpdatedAt, state.ClientUpdatedAt);
        Assert.Equal(0, state.CurrentClientCount);
        Assert.Equal(now, state.SubscriptionUpdatedAt);
    }

    [Fact]
    public async Task Archive_AlreadyArchived_DoesNotChangeCounter()
    {
        var seed = await SeedPersistedClientAsync(isActive: false, withSubscription: true);
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ArchiveAsync(
            seed.ClientId,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(ArchiveClientStoreOutcome.AlreadyArchived, outcome);
        Assert.False(state.IsActive);
        Assert.Equal(0, state.CurrentClientCount);
    }

    [Fact]
    public async Task Archive_MissingSubscription_RollsBackClientState()
    {
        var seed = await SeedPersistedClientAsync(isActive: true, withSubscription: false);
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ArchiveAsync(
            seed.ClientId,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(ArchiveClientStoreOutcome.SubscriptionMissing, outcome);
        Assert.True(state.IsActive);
    }

    [Fact]
    public async Task Reactivate_AtLimit_RollsBackClientState()
    {
        var seed = await SeedPersistedClientAsync(
            isActive: false,
            withSubscription: true,
            clientLimit: 5,
            currentClientCount: 5);
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ReactivateAsync(
            seed.ClientId,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(ReactivateClientStoreOutcome.ClientLimitReached, outcome);
        Assert.False(state.IsActive);
        Assert.Equal(5, state.CurrentClientCount);
    }

    [Theory]
    [InlineData("INACTIVE", ReactivateClientStoreOutcome.SubscriptionInactive)]
    [InlineData("SUSPENDED", ReactivateClientStoreOutcome.SubscriptionSuspended)]
    [InlineData("CANCELLED", ReactivateClientStoreOutcome.SubscriptionCancelled)]
    public async Task Reactivate_BlockedStatus_RollsBackClientState(
        string statusValue,
        ReactivateClientStoreOutcome expectedOutcome)
    {
        var seed = await SeedPersistedClientAsync(
            isActive: false,
            withSubscription: true,
            status: SubscriptionStatus.FromString(statusValue));
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ReactivateAsync(
            seed.ClientId,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(expectedOutcome, outcome);
        Assert.False(state.IsActive);
        Assert.Equal(0, state.CurrentClientCount);
    }

    [Fact]
    public async Task Reactivate_ExemptSubscription_IgnoresStatusAndLimit()
    {
        var seed = await SeedPersistedClientAsync(
            isActive: false,
            withSubscription: true,
            status: SubscriptionStatus.Cancelled,
            clientLimit: 5,
            currentClientCount: 5,
            isExemptFromBilling: true);
        var now = ClientPersistenceTestData.NowUtc.AddHours(1);
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ReactivateAsync(
            seed.ClientId,
            seed.TrainerId,
            now,
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(ReactivateClientStoreOutcome.Reactivated, outcome);
        Assert.True(state.IsActive);
        Assert.Equal(6, state.CurrentClientCount);
        Assert.Equal(now, state.SubscriptionUpdatedAt);
    }

    [Fact]
    public async Task Reactivate_MissingSubscription_RollsBackClientState()
    {
        var seed = await SeedPersistedClientAsync(isActive: false, withSubscription: false);
        await using var context = _contextFactory.Create(seed.TrainerId);

        var outcome = await CreateStore(context).ReactivateAsync(
            seed.ClientId,
            seed.TrainerId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerId, seed.ClientId);
        Assert.Equal(ReactivateClientStoreOutcome.SubscriptionMissing, outcome);
        Assert.False(state.IsActive);
    }

    [Fact]
    public async Task GetForUpdate_OtherTenant_ReturnsNull()
    {
        var seed = await SeedTwoTenantsAsync(clientBIsActive: true);
        await using var context = _contextFactory.Create(seed.TrainerAId);

        var result = await CreateStore(context).GetForUpdateAsync(
            seed.ClientBId,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Archive_OtherTenant_ReturnsNotFoundWithoutChangingState()
    {
        var seed = await SeedTwoTenantsAsync(clientBIsActive: true);
        await using var context = _contextFactory.Create(seed.TrainerAId);

        var outcome = await CreateStore(context).ArchiveAsync(
            seed.ClientBId,
            seed.TrainerAId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerBId, seed.ClientBId);
        Assert.Equal(ArchiveClientStoreOutcome.NotFound, outcome);
        Assert.True(state.IsActive);
        Assert.Equal(1, state.CurrentClientCount);
    }

    [Fact]
    public async Task Reactivate_OtherTenant_ReturnsNotFoundWithoutChangingState()
    {
        var seed = await SeedTwoTenantsAsync(clientBIsActive: false);
        await using var context = _contextFactory.Create(seed.TrainerAId);

        var outcome = await CreateStore(context).ReactivateAsync(
            seed.ClientBId,
            seed.TrainerAId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            CancellationToken.None);

        var state = await ReadStateAsync(seed.TrainerBId, seed.ClientBId);
        Assert.Equal(ReactivateClientStoreOutcome.NotFound, outcome);
        Assert.False(state.IsActive);
        Assert.Equal(0, state.CurrentClientCount);
    }

    private static ClientStore CreateStore(Infrastructure.Data.PtManagerDbContext context)
    {
        return new ClientStore(context, new PostgresConstraintTranslator());
    }

    private async Task<CreateSeed> SeedCreateAsync(
        SubscriptionStatus? status = null,
        int clientLimit = 5,
        int currentClientCount = 0)
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            status ?? SubscriptionStatus.Active,
            clientLimit,
            currentClientCount);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription);
        return new CreateSeed(trainer.Id);
    }

    private async Task<PersistedClientSeed> SeedPersistedClientAsync(
        bool isActive,
        bool withSubscription,
        SubscriptionStatus? status = null,
        int clientLimit = 5,
        int? currentClientCount = null,
        bool isExemptFromBilling = false)
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var client = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            discriminator,
            isActive);

        if (!withSubscription)
        {
            await ClientPersistenceTestData.PersistAsync(
                _fixture,
                trainer.Id,
                trainer,
                client);
            return new PersistedClientSeed(trainer.Id, client.Id);
        }

        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            status ?? SubscriptionStatus.Active,
            clientLimit,
            currentClientCount ?? (isActive ? 1 : 0),
            isExemptFromBilling);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription,
            client);
        return new PersistedClientSeed(trainer.Id, client.Id);
    }

    private async Task<TwoTenantSeed> SeedTwoTenantsAsync(bool clientBIsActive)
    {
        var discriminator = NewDiscriminator();
        var trainerA = ClientPersistenceTestData.CreateTrainer($"a-{discriminator}");
        var trainerB = ClientPersistenceTestData.CreateTrainer($"b-{discriminator}");
        var subscriptionA = ClientPersistenceTestData.CreateSubscription(
            trainerA.Id,
            SubscriptionStatus.Active,
            5,
            0);
        var subscriptionB = ClientPersistenceTestData.CreateSubscription(
            trainerB.Id,
            SubscriptionStatus.Active,
            5,
            clientBIsActive ? 1 : 0);
        var clientB = ClientPersistenceTestData.CreateClient(
            trainerB.Id,
            discriminator,
            clientBIsActive);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainerA.Id,
            trainerA,
            subscriptionA);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainerB.Id,
            trainerB,
            subscriptionB,
            clientB);
        return new TwoTenantSeed(trainerA.Id, trainerB.Id, clientB.Id);
    }

    private async Task AssertDuplicateProfileAsync(
        SaveClientProfileOutcome expectedOutcome,
        Action<Client, Client, DateTime> applyDuplicate)
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainer.Id,
            SubscriptionStatus.Active,
            5,
            2);
        var first = ClientPersistenceTestData.CreateClient(trainer.Id, $"first-{discriminator}");
        var second = ClientPersistenceTestData.CreateClient(trainer.Id, $"second-{discriminator}");
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            subscription,
            first,
            second);
        var originalEmail = second.ContactEmail;
        var originalPhone = second.Phone;
        await using var context = _contextFactory.Create(trainer.Id);
        var store = CreateStore(context);
        var trackedSecond = await store.GetForUpdateAsync(second.Id, CancellationToken.None);
        Assert.NotNull(trackedSecond);
        applyDuplicate(first, trackedSecond, ClientPersistenceTestData.NowUtc.AddHours(1));

        var outcome = await store.SaveProfileAsync(trackedSecond, CancellationToken.None);

        await using var readContext = _fixture.CreateContext(trainer.Id);
        var persisted = await readContext.Clients.SingleAsync(
            client => client.Id == second.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(expectedOutcome, outcome);
        Assert.Equal(originalEmail, persisted.ContactEmail);
        Assert.Equal(originalPhone, persisted.Phone);
    }

    private async Task<ClientState> ReadStateAsync(Guid trainerId, Guid clientId)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var client = await context.Clients
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == clientId,
                TestContext.Current.CancellationToken);
        var subscription = await context.TrainerSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TrainerId == trainerId,
                TestContext.Current.CancellationToken);
        return new ClientState(
            client is not null,
            client?.IsActive ?? false,
            client?.IsDeleted ?? false,
            client?.UpdatedAt,
            subscription?.CurrentClientCount,
            subscription?.UpdatedAt);
    }

    private static string NewDiscriminator() => Guid.NewGuid().ToString("N");

    private sealed record CreateSeed(Guid TrainerId);

    private sealed record PersistedClientSeed(Guid TrainerId, Guid ClientId);

    private sealed record TwoTenantSeed(Guid TrainerAId, Guid TrainerBId, Guid ClientBId);

    private sealed record ClientState(
        bool ClientExists,
        bool IsActive,
        bool IsDeleted,
        DateTime? ClientUpdatedAt,
        int? CurrentClientCount,
        DateTime? SubscriptionUpdatedAt);
}
