using Application.Common.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Features.Packs.ClientSessionPacks.GetClientSessionPack;
using Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;
using Application.Features.Packs.ClientSessionPacks.ListUsableClientSessionPacks;
using Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Features.Packs.PackTypes.GetPackType;
using Application.Features.Packs.PackTypes.ListPackTypes;
using Application.Features.Packs.PackTypes.ReactivatePackType;
using Application.Features.Packs.PackTypes.UpdatePackType;
using Application.Pagination;
using Domain.Entities.Billing;

namespace Application.UnitTests.Features.Packs;

public sealed class PackAdditionalHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid PackId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPackType_MissingTenant_DoesNotQuery()
    {
        var queries = new FakePackTypeQueries();
        var handler = new GetPackTypeHandler(new TenantStub(null), queries);

        var result = await handler.HandleAsync(
            new GetPackTypeQuery(PackId),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, queries.GetCalls);
    }

    [Fact]
    public async Task GetPackType_MissingPack_ReturnsNotFound()
    {
        var handler = new GetPackTypeHandler(
            new TenantStub(TrainerId),
            new FakePackTypeQueries()
        );

        var result = await handler.HandleAsync(
            new GetPackTypeQuery(PackId),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("pack_type_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ListPackTypes_ValidQuery_NormalizesSearchAndPaging()
    {
        var queries = new FakePackTypeQueries();
        var handler = new ListPackTypesHandler(
            new ListPackTypesQueryValidator(),
            new TenantStub(TrainerId),
            queries
        );

        var result = await handler.HandleAsync(
            new ListPackTypesQuery("  strength  ", PackTypeActivityFilter.All, 2, 25),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, queries.LastTrainerId);
        Assert.Equal("strength", queries.LastSearch);
        Assert.Equal(new PageRequest(2, 25), queries.LastPage);
    }

    [Fact]
    public async Task UpdatePackType_NotFound_ReturnsNotFound()
    {
        var handler = new UpdatePackTypeHandler(
            new UpdatePackTypeCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            new FakePackTypeStore()
        );

        var result = await handler.HandleAsync(
            new UpdatePackTypeCommand(PackId, "Pack", 10, 10000, "EUR", 30),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("pack_type_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ReactivatePackType_AlreadyActive_ReturnsSuccess()
    {
        var store = new FakePackTypeStore
        {
            ActiveOutcome = PackTypeStoreResult.ForAlreadyInRequested()
        };
        var handler = new ReactivatePackTypeHandler(
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store
        );

        var result = await handler.HandleAsync(
            new ReactivatePackTypeCommand(PackId),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.True(store.LastIsActive);
    }

    [Fact]
    public async Task GetClientSessionPack_MissingPack_ReturnsNotFound()
    {
        var handler = new GetClientSessionPackHandler(
            new TenantStub(TrainerId),
            new FakeClientSessionPackQueries()
        );

        var result = await handler.HandleAsync(
            new GetClientSessionPackQuery(PackId),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("client_session_pack_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ListClientSessionPacks_ValidQuery_ForwardsFilterAndPaging()
    {
        var queries = new FakeClientSessionPackQueries();
        var handler = new ListClientSessionPacksHandler(
            new ListClientSessionPacksQueryValidator(),
            new TenantStub(TrainerId),
            queries
        );

        var result = await handler.HandleAsync(
            new ListClientSessionPacksQuery(
                ClientId,
                ClientSessionPackActivityFilter.Completed,
                3,
                20
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, queries.LastTrainerId);
        Assert.Equal(ClientId, queries.LastClientId);
        Assert.Equal(ClientSessionPackActivityFilter.Completed, queries.LastActivity);
        Assert.Equal(new PageRequest(3, 20), queries.LastPage);
    }

    [Fact]
    public async Task ListUsableClientSessionPacks_QueryResult_PreservesResult()
    {
        IReadOnlyList<ClientSessionPackDto> expected = [CreateClientPackDto()];
        var queries = new FakeClientSessionPackQueries { UsableResult = expected };
        var handler = new ListUsableClientSessionPacksHandler(
            new TenantStub(TrainerId),
            queries
        );

        var result = await handler.HandleAsync(
            new ListUsableClientSessionPacksQuery(ClientId),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        Assert.Equal(TrainerId, queries.LastTrainerId);
        Assert.Equal(ClientId, queries.LastClientId);
    }

    [Fact]
    public async Task UpdateExpectedEndDate_BeforePurchase_ReturnsValidationError()
    {
        var store = new FakeClientSessionPackStore
        {
            UpdateOutcome = ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.ExpectedEndDateBeforePurchase
            )
        };
        var handler = CreateUpdateExpectedEndDateHandler(store);

        var result = await handler.HandleAsync(
            new UpdateClientSessionPackExpectedEndDateCommand(
                PackId,
                new DateOnly(2026, 8, 1)
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Collection(
            result.Error.ValidationErrors,
            error => Assert.Equal("expected_end_date_before_purchase", error.Code)
        );
    }

    [Fact]
    public async Task UpdateExpectedEndDate_RepeatedValue_ReturnsCurrentPack()
    {
        var pack = CreateClientPack();
        var store = new FakeClientSessionPackStore
        {
            UpdateOutcome = ClientSessionPackStoreResult.ForAlreadyInRequested(pack)
        };
        var handler = CreateUpdateExpectedEndDateHandler(store);

        var result = await handler.HandleAsync(
            new UpdateClientSessionPackExpectedEndDateCommand(
                pack.Id,
                pack.ExpectedEndDate
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(pack.Id, result.Value.Id);
    }

    private static UpdateClientSessionPackExpectedEndDateHandler
        CreateUpdateExpectedEndDateHandler(FakeClientSessionPackStore store) => new(
            new UpdateClientSessionPackExpectedEndDateCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store
        );

    private static ClientSessionPack CreateClientPack()
    {
        var type = new PackType(TrainerId, "Pack", 10, 10000, "EUR", 30, Now);
        return new ClientSessionPack(
            TrainerId,
            ClientId,
            type,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1),
            Now
        );
    }

    private static ClientSessionPackDto CreateClientPackDto() => new(
        PackId,
        ClientId,
        Guid.NewGuid(),
        "Pack",
        10,
        10,
        10000,
        "EUR",
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 9, 1),
        false,
        null,
        false,
        Now,
        Now
    );

    private sealed class FakePackTypeQueries : IPackTypeQueries
    {
        public int GetCalls { get; private set; }
        public Guid LastTrainerId { get; private set; }
        public string? LastSearch { get; private set; }
        public PageRequest? LastPage { get; private set; }

        public Task<PackTypeDto?> GetAsync(
            Guid trainerId,
            Guid packTypeId,
            CancellationToken cancellationToken
        )
        {
            GetCalls++;
            return Task.FromResult<PackTypeDto?>(null);
        }

        public Task<PageResult<PackTypeDto>> ListAsync(
            Guid trainerId,
            string? search,
            PackTypeActivityFilter activity,
            PageRequest page,
            CancellationToken cancellationToken
        )
        {
            LastTrainerId = trainerId;
            LastSearch = search;
            LastPage = page;
            return Task.FromResult(new PageResult<PackTypeDto>([], 0));
        }
    }

    private sealed class FakePackTypeStore : IPackTypeStore
    {
        public PackTypeStoreResult ActiveOutcome { get; init; } =
            PackTypeStoreResult.ForNotFound();
        public bool LastIsActive { get; private set; }

        public Task AddAsync(PackType packType, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<PackTypeStoreResult> UpdateAsync(
            Guid packTypeId,
            Guid trainerId,
            string name,
            int sessionCount,
            int priceCents,
            string currency,
            int? expectedDurationDays,
            DateTime now,
            CancellationToken cancellationToken
        ) => Task.FromResult(PackTypeStoreResult.ForNotFound());

        public Task<PackTypeStoreResult> SetActiveAsync(
            Guid packTypeId,
            Guid trainerId,
            bool isActive,
            DateTime now,
            CancellationToken cancellationToken
        )
        {
            LastIsActive = isActive;
            return Task.FromResult(ActiveOutcome);
        }
    }

    private sealed class FakeClientSessionPackQueries : IClientSessionPackQueries
    {
        public IReadOnlyList<ClientSessionPackDto> UsableResult { get; init; } = [];
        public Guid LastTrainerId { get; private set; }
        public Guid? LastClientId { get; private set; }
        public ClientSessionPackActivityFilter LastActivity { get; private set; }
        public PageRequest? LastPage { get; private set; }

        public Task<ClientSessionPackDto?> GetAsync(
            Guid trainerId,
            Guid packId,
            CancellationToken cancellationToken
        ) => Task.FromResult<ClientSessionPackDto?>(null);

        public Task<PageResult<ClientSessionPackDto>> ListAsync(
            Guid trainerId,
            Guid? clientId,
            ClientSessionPackActivityFilter activity,
            PageRequest page,
            CancellationToken cancellationToken
        )
        {
            LastTrainerId = trainerId;
            LastClientId = clientId;
            LastActivity = activity;
            LastPage = page;
            return Task.FromResult(new PageResult<ClientSessionPackDto>([], 0));
        }

        public Task<IReadOnlyList<ClientSessionPackDto>> ListUsableAsync(
            Guid trainerId,
            Guid clientId,
            CancellationToken cancellationToken
        )
        {
            LastTrainerId = trainerId;
            LastClientId = clientId;
            return Task.FromResult(UsableResult);
        }
    }

    private sealed class FakeClientSessionPackStore : IClientSessionPackStore
    {
        public ClientSessionPackStoreResult UpdateOutcome { get; init; } =
            ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.PackNotFound
            );

        public Task<ClientSessionPackStoreResult> AssignAsync(
            Guid trainerId,
            Guid clientId,
            Guid packTypeId,
            DateOnly purchaseDate,
            DateOnly? expectedEndDate,
            DateTime now,
            CancellationToken cancellationToken
        ) => Task.FromResult(ClientSessionPackStoreResult.For(
            ClientSessionPackStoreResult.Status.ClientNotFound
        ));

        public Task<ClientSessionPackStoreResult> UpdateExpectedEndDateAsync(
            Guid trainerId,
            Guid packId,
            DateOnly? expectedEndDate,
            DateTime now,
            CancellationToken cancellationToken
        ) => Task.FromResult(UpdateOutcome);

        public Task<ClientSessionPackStoreResult> CancelAsync(
            Guid trainerId,
            Guid packId,
            DateTime now,
            CancellationToken cancellationToken
        ) => Task.FromResult(ClientSessionPackStoreResult.For(
            ClientSessionPackStoreResult.Status.PackNotFound
        ));
    }

    private sealed class TenantStub(Guid? trainerId) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId => null;
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class ClockStub(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
