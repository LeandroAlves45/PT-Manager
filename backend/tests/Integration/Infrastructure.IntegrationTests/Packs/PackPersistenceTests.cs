using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Domain.Entities.Sessions;
using Infrastructure.IntegrationTests.Clients;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Packs;
using Npgsql;

namespace Infrastructure.IntegrationTests.Packs;

[Collection(PostgresCollection.Name)]
public sealed class PackPersistenceTests
{
    private readonly PostgresContainerFixture _fixture;

    public PackPersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Assign_Twice_AllowsMultipleUsablePacksWithSnapshot()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new ClientSessionPackStore(context);

        var first = await store.AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1),
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );
        var second = await store.AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 8, 2),
            null,
            ClientPersistenceTestData.NowUtc.AddMinutes(1),
            TestContext.Current.CancellationToken
        );

        var usable = await new ClientSessionPackQueries(context).ListUsableAsync(
            seed.TrainerId,
            seed.ClientId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ClientSessionPackStoreResult.Status.Assigned, first.Kind);
        Assert.Equal(ClientSessionPackStoreResult.Status.Assigned, second.Kind);
        Assert.Equal(2, usable.Count);
        Assert.Equal(first.Pack!.Id, usable[0].Id);
        Assert.Equal(second.Pack!.Id, usable[1].Id);
        Assert.Equal("EUR", first.Pack.Currency);
    }

    [Fact]
    public async Task ListUsable_PastExpectedEndDate_RemainsVisible()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new ClientSessionPackStore(context);
        var assigned = await store.AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );

        var result = await new ClientSessionPackQueries(context).ListUsableAsync(
            seed.TrainerId,
            seed.ClientId,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(result, pack => pack.Id == assigned.Pack!.Id);
    }

    [Fact]
    public async Task ListUsable_ClientAndPackQueries_ReturnIdenticalStableOrder()
    {
        var seed = await SeedAsync();
        var packType = ClientPersistenceTestData.CreatePackType(
            seed.TrainerId,
            Guid.NewGuid().ToString("N"));
        var purchaseDate = new DateOnly(2026, 8, 1);
        var sameCreatedAt = ClientPersistenceTestData.NowUtc.AddMinutes(1);
        var packs = new[]
        {
            ClientPersistenceTestData.CreatePack(
                seed.TrainerId, seed.ClientId, packType, purchaseDate,
                purchaseDate.AddDays(5), now: sameCreatedAt),
            ClientPersistenceTestData.CreatePack(
                seed.TrainerId, seed.ClientId, packType, purchaseDate,
                purchaseDate.AddDays(10), now: sameCreatedAt),
            ClientPersistenceTestData.CreatePack(
                seed.TrainerId, seed.ClientId, packType, purchaseDate,
                purchaseDate.AddDays(10), now: sameCreatedAt),
            ClientPersistenceTestData.CreatePack(
                seed.TrainerId, seed.ClientId, packType, purchaseDate,
                expectedEndDate: null, now: sameCreatedAt.AddMinutes(1))
        };
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            seed.TrainerId,
            packType,
            packs[0],
            packs[1],
            packs[2],
            packs[3]);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var clientOrder = await new ClientQueries(context).ListUsablePacksAsync(
            seed.ClientId,
            TestContext.Current.CancellationToken);
        var packOrder = await new ClientSessionPackQueries(context).ListUsableAsync(
            seed.TrainerId,
            seed.ClientId,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            clientOrder.Select(pack => pack.Id),
            packOrder.Select(pack => pack.Id));
    }

    [Fact]
    public async Task Assign_ArchivedClient_ReturnsClientInactive()
    {
        var seed = await SeedAsync(clientActive: false);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var outcome = await new ClientSessionPackStore(context).AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 8, 1),
            null,
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            ClientSessionPackStoreResult.Status.ClientInactive,
            outcome.Kind
        );
    }

    [Fact]
    public async Task Assign_ArchivedPackType_ReturnsPackTypeInactive()
    {
        var seed = await SeedAsync(packTypeActive: false);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var outcome = await new ClientSessionPackStore(context).AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 8, 1),
            null,
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            ClientSessionPackStoreResult.Status.PackTypeInactive,
            outcome.Kind
        );
    }

    [Fact]
    public async Task Cancel_UnusedAndUnreferenced_IsIdempotent()
    {
        var seed = await SeedAsync();
        Guid packId;
        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var assigned = await new ClientSessionPackStore(context).AssignAsync(
                seed.TrainerId,
                seed.ClientId,
                seed.PackTypeId,
                new DateOnly(2026, 8, 1),
                null,
                ClientPersistenceTestData.NowUtc,
                TestContext.Current.CancellationToken
            );
            packId = assigned.Pack!.Id;
        }

        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        var first = await new ClientSessionPackStore(firstContext).CancelAsync(
            seed.TrainerId,
            packId,
            ClientPersistenceTestData.NowUtc.AddMinutes(1),
            TestContext.Current.CancellationToken
        );
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);
        var second = await new ClientSessionPackStore(secondContext).CancelAsync(
            seed.TrainerId,
            packId,
            ClientPersistenceTestData.NowUtc.AddMinutes(2),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ClientSessionPackStoreResult.Status.Cancelled, first.Kind);
        Assert.Equal(
            ClientSessionPackStoreResult.Status.AlreadyInRequestedState,
            second.Kind
        );
    }

    [Fact]
    public async Task Cancel_ReferencedByScheduledSession_ReturnsConflictOutcome()
    {
        var seed = await SeedAsync();
        Guid packId;
        await using (var setup = _fixture.CreateContext(seed.TrainerId))
        {
            var assigned = await new ClientSessionPackStore(setup).AssignAsync(
                seed.TrainerId,
                seed.ClientId,
                seed.PackTypeId,
                new DateOnly(2026, 8, 1),
                null,
                ClientPersistenceTestData.NowUtc,
                TestContext.Current.CancellationToken
            );
            packId = assigned.Pack!.Id;
            setup.Sessions.Add(new Session(
                seed.TrainerId,
                seed.ClientId,
                packId,
                ClientPersistenceTestData.NowUtc.AddDays(1),
                60,
                null,
                null,
                null,
                ClientPersistenceTestData.NowUtc
            ));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var outcome = await new ClientSessionPackStore(context).CancelAsync(
            seed.TrainerId,
            packId,
            ClientPersistenceTestData.NowUtc.AddMinutes(1),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            ClientSessionPackStoreResult.Status.PackReferenced,
            outcome.Kind
        );
    }

    [Fact]
    public async Task Cancel_UsedPack_ReturnsPackUsed()
    {
        var seed = await SeedAsync();
        var packType = ClientPersistenceTestData.CreatePackType(
            seed.TrainerId,
            Guid.NewGuid().ToString("N")
        );
        var pack = ClientPersistenceTestData.CreatePack(
            seed.TrainerId,
            seed.ClientId,
            packType,
            new DateOnly(2026, 8, 1),
            null,
            sessionsToConsume: 1
        );
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            seed.TrainerId,
            packType,
            pack
        );
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var outcome = await new ClientSessionPackStore(context).CancelAsync(
            seed.TrainerId,
            pack.Id,
            ClientPersistenceTestData.NowUtc.AddMinutes(2),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ClientSessionPackStoreResult.Status.PackUsed, outcome.Kind);
    }

    [Fact]
    public async Task UpdateExpectedEndDate_BeforePurchase_ReturnsExpectedOutcome()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new ClientSessionPackStore(context);
        var assigned = await store.AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 8, 10),
            null,
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );

        var outcome = await store.UpdateExpectedEndDateAsync(
            seed.TrainerId,
            assigned.Pack!.Id,
            new DateOnly(2026, 8, 9),
            ClientPersistenceTestData.NowUtc.AddMinutes(1),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            ClientSessionPackStoreResult.Status.ExpectedEndDateBeforePurchase,
            outcome.Kind
        );
    }

    [Fact]
    public async Task UpdateExpectedEndDate_RepeatedValue_IsIdempotent()
    {
        var seed = await SeedAsync();
        var expectedEndDate = new DateOnly(2026, 9, 1);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new ClientSessionPackStore(context);
        var assigned = await store.AssignAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackTypeId,
            new DateOnly(2026, 8, 1),
            expectedEndDate,
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );

        var outcome = await store.UpdateExpectedEndDateAsync(
            seed.TrainerId,
            assigned.Pack!.Id,
            expectedEndDate,
            ClientPersistenceTestData.NowUtc.AddMinutes(1),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            ClientSessionPackStoreResult.Status.AlreadyInRequestedState,
            outcome.Kind
        );
        Assert.Equal(ClientPersistenceTestData.NowUtc, outcome.Pack!.UpdatedAt);
    }

    [Fact]
    public async Task Get_OtherTenant_ReturnsNull()
    {
        var owner = await SeedAsync();
        var requester = await SeedAsync();
        await using var ownerContext = _fixture.CreateContext(owner.TrainerId);
        var assigned = await new ClientSessionPackStore(ownerContext).AssignAsync(
            owner.TrainerId,
            owner.ClientId,
            owner.PackTypeId,
            new DateOnly(2026, 8, 1),
            null,
            ClientPersistenceTestData.NowUtc,
            TestContext.Current.CancellationToken
        );
        await using var requesterContext = _fixture.CreateContext(requester.TrainerId);

        var result = await new ClientSessionPackQueries(requesterContext).GetAsync(
            requester.TrainerId,
            assigned.Pack!.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ExpectedEndDate_BeforePurchase_IsRejectedByDatabase()
    {
        var seed = await SeedAsync();
        const string sql = """
            INSERT INTO client_session_packs (
                id, owner_trainer_id, client_id, pack_type_id, pack_name,
                total_sessions, sessions_remaining, price_cents, currency,
                purchase_date, expected_end_date, completed_at, is_deleted,
                created_at, updated_at)
            VALUES (
                @id, @trainer_id, @client_id, @pack_type_id, 'Pack',
                10, 10, 10000, 'EUR', DATE '2026-08-10', DATE '2026-08-09',
                NULL, false, @now, @now);
        """;

        var action = () => _fixture.ExecuteSqlAsync(
            sql,
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("trainer_id", seed.TrainerId),
            new NpgsqlParameter("client_id", seed.ClientId),
            new NpgsqlParameter("pack_type_id", seed.PackTypeId),
            new NpgsqlParameter("now", ClientPersistenceTestData.NowUtc)
        );

        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(
            "ck_client_session_packs_expected_end_order",
            exception.ConstraintName
        );
    }

    [Fact]
    public async Task ZeroBalance_WithoutCompletedAt_IsRejectedByDatabase()
    {
        var seed = await SeedAsync();
        const string sql = """
            INSERT INTO client_session_packs (
                id, owner_trainer_id, client_id, pack_type_id, pack_name,
                total_sessions, sessions_remaining, price_cents, currency,
                purchase_date, expected_end_date, completed_at, is_deleted,
                created_at, updated_at)
            VALUES (
                @id, @trainer_id, @client_id, @pack_type_id, 'Pack',
                10, 0, 10000, 'EUR', DATE '2026-08-10', NULL,
                NULL, false, @now, @now);
        """;

        var action = () => _fixture.ExecuteSqlAsync(
            sql,
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("trainer_id", seed.TrainerId),
            new NpgsqlParameter("client_id", seed.ClientId),
            new NpgsqlParameter("pack_type_id", seed.PackTypeId),
            new NpgsqlParameter("now", ClientPersistenceTestData.NowUtc)
        );

        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(
            "ck_client_session_packs_completion_consistency",
            exception.ConstraintName
        );
    }

    private async Task<Seed> SeedAsync(
        bool clientActive = true,
        bool packTypeActive = true
    )
    {
        var discriminator = Guid.NewGuid().ToString("N");
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var client = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            discriminator,
            clientActive
        );
        var packType = ClientPersistenceTestData.CreatePackType(
            trainer.Id,
            discriminator
        );
        if (!packTypeActive)
            packType.Archive(ClientPersistenceTestData.NowUtc.AddMinutes(1));

        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            client,
            packType
        );
        return new Seed(trainer.Id, client.Id, packType.Id);
    }

    private sealed record Seed(
        Guid TrainerId,
        Guid ClientId,
        Guid PackTypeId
    );
}
