using Application.Common.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;
using Application.Features.Packs.ClientSessionPacks.CancelClientSessionPack;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.ArchivePackType;
using Application.Features.Packs.PackTypes.CreatePackType;
using Domain.Entities.Billing;

namespace Application.UnitTests.Features.Packs;

public sealed class PackHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 14, 23, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreatePackType_ValidCommand_UsesEffectiveTenant()
    {
        var store = new FakePackTypeStore();
        var handler = new CreatePackTypeHandler(
            new CreatePackTypeCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store
        );

        var result = await handler.HandleAsync(
            new CreatePackTypeCommand("Pack", 10, 10000, "eur", 30),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, store.Added!.OwnerTrainerId);
        Assert.Equal("EUR", result.Value.Currency);
    }

    [Fact]
    public async Task CreatePackType_MissingTenant_DoesNotWrite()
    {
        var store = new FakePackTypeStore();
        var handler = new CreatePackTypeHandler(
            new CreatePackTypeCommandValidator(),
            new TenantStub(null),
            new ClockStub(Now),
            store
        );

        var result = await handler.HandleAsync(
            new CreatePackTypeCommand("Pack", 10, 10000, "EUR", null),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsFailure);
        Assert.Null(store.Added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchivePackType_ChangedOrRepeated_ReturnsSuccess(bool changed)
    {
        var store = new FakePackTypeStore
        {
            ActiveOutcome = changed
                ? PackTypeStoreResult.ForChanged()
                : PackTypeStoreResult.ForAlreadyInRequested()
        };
        var handler = new ArchivePackTypeHandler(
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store
        );

        var result = await handler.HandleAsync(
            new ArchivePackTypeCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Assign_PurchaseDateFutureInTrainerTimezone_DoesNotWrite()
    {
        var store = new FakeClientSessionPackStore();
        var handler = CreateAssignHandler(
            store,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")
        );

        var result = await handler.HandleAsync(
            new AssignClientSessionPackCommand(
                ClientId,
                Guid.NewGuid(),
                new DateOnly(2026, 8, 16),
                null
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Equal(0, store.AssignCalls);
    }

    [Fact]
    public async Task Assign_ArchivedClient_ReturnsConflict()
    {
        var store = new FakeClientSessionPackStore
        {
            AssignOutcome = ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.ClientInactive
            )
        };

        var result = await CreateAssignHandler(store, TimeZoneInfo.Utc)
            .HandleAsync(
                ValidAssignCommand(),
                TestContext.Current.CancellationToken
            );

        Assert.Equal("client_inactive", result.Error!.Code);
    }

    [Fact]
    public async Task Assign_InactivePackType_ReturnsConflict()
    {
        var store = new FakeClientSessionPackStore
        {
            AssignOutcome = ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.PackTypeInactive
            )
        };

        var result = await CreateAssignHandler(store, TimeZoneInfo.Utc)
            .HandleAsync(
                ValidAssignCommand(),
                TestContext.Current.CancellationToken
            );

        Assert.Equal("pack_type_inactive", result.Error!.Code);
    }

    [Theory]
    [InlineData(ClientSessionPackStoreResult.Status.Cancelled, null)]
    [InlineData(ClientSessionPackStoreResult.Status.AlreadyInRequestedState, null)]
    [InlineData(ClientSessionPackStoreResult.Status.PackUsed, "client_session_pack_used")]
    [InlineData(ClientSessionPackStoreResult.Status.PackReferenced, "client_session_pack_referenced")]
    public async Task Cancel_TranslatesStoreOutcome(
        ClientSessionPackStoreResult.Status status,
        string? expectedError
    )
    {
        var store = new FakeClientSessionPackStore
        {
            CancelOutcome = ClientSessionPackStoreResult.For(status)
        };
        var handler = new CancelClientSessionPackHandler(
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store
        );

        var result = await handler.HandleAsync(
            new CancelClientSessionPackCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(expectedError is null, result.IsSuccess);
        if (expectedError is not null)
            Assert.Equal(expectedError, result.Error!.Code);
    }

    private static AssignClientSessionPackHandler CreateAssignHandler(
        FakeClientSessionPackStore store,
        TimeZoneInfo timeZone
    ) => new(
        new AssignClientSessionPackCommandValidator(),
        new TenantStub(TrainerId),
        new TimeZoneProviderStub(timeZone),
        new ClockStub(Now),
        store
    );

    private static AssignClientSessionPackCommand ValidAssignCommand() => new(
        ClientId,
        Guid.NewGuid(),
        new DateOnly(2026, 8, 14),
        new DateOnly(2026, 9, 14)
    );

    private sealed class FakePackTypeStore : IPackTypeStore
    {
        public PackType? Added { get; private set; }
        public PackTypeStoreResult ActiveOutcome { get; init; } =
            PackTypeStoreResult.ForChanged();

        public Task AddAsync(PackType packType, CancellationToken cancellationToken)
        {
            Added = packType;
            return Task.CompletedTask;
        }

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
        ) => Task.FromResult(ActiveOutcome);
    }

    private sealed class FakeClientSessionPackStore : IClientSessionPackStore
    {
        public int AssignCalls { get; private set; }
        public ClientSessionPackStoreResult AssignOutcome { get; init; } =
            ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.ClientNotFound
            );
        public ClientSessionPackStoreResult CancelOutcome { get; init; } =
            ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.Cancelled
            );

        public Task<ClientSessionPackStoreResult> AssignAsync(
            Guid trainerId,
            Guid clientId,
            Guid packTypeId,
            DateOnly purchaseDate,
            DateOnly? expectedEndDate,
            DateTime now,
            CancellationToken cancellationToken
        )
        {
            AssignCalls++;
            return Task.FromResult(AssignOutcome);
        }

        public Task<ClientSessionPackStoreResult> UpdateExpectedEndDateAsync(
            Guid trainerId,
            Guid packId,
            DateOnly? expectedEndDate,
            DateTime now,
            CancellationToken cancellationToken
        ) => Task.FromResult(ClientSessionPackStoreResult.For(
            ClientSessionPackStoreResult.Status.PackNotFound
        ));

        public Task<ClientSessionPackStoreResult> CancelAsync(
            Guid trainerId,
            Guid packId,
            DateTime now,
            CancellationToken cancellationToken
        ) => Task.FromResult(CancelOutcome);
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

    private sealed class TimeZoneProviderStub(TimeZoneInfo timeZone)
        : ITrainerTimeZoneProvider
    {
        public Task<TimeZoneInfo> GetRequiredAsync(
            Guid trainerId,
            CancellationToken cancellationToken
        ) => Task.FromResult(timeZone);
    }
}
