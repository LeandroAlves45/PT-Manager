using Application.Errors;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.ArchiveClient;
using Application.Features.Clients.CreateClient;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.GetClient;
using Application.Features.Clients.ListClients;
using Application.Features.Clients.ReactivateClient;
using Application.Features.Clients.UpdateClient;
using Application.Pagination;

namespace Application.UnitTests.Features.Clients;

/// <summary>Verifica a orquestração e o mapping de outcomes dos seis handlers de Clients.</summary>
public sealed class ClientHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private readonly StubClock _clock = new() { UtcNow = ClientTestData.NowUtc };

    [Fact]
    public async Task Create_InvalidCommand_DoesNotCallStore()
    {
        var store = new FakeClientStore();
        var handler = CreateCreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidCommand() with { Name = string.Empty },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Validation, result.Error!.Category);
        Assert.Equal(0, store.CreateCalls);
    }

    [Fact]
    public async Task Create_MissingTenant_FailsClosed()
    {
        var store = new FakeClientStore();
        var handler = CreateCreateHandler(store, new StubTenantContext());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidCommand(),
            CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, store.CreateCalls);
    }

    [Theory]
    [InlineData(CreateClientStoreOutcome.DuplicateEmail, "client_email_already_exists", ErrorCategory.Conflict)]
    [InlineData(CreateClientStoreOutcome.DuplicatePhone, "client_phone_already_exists", ErrorCategory.Conflict)]
    [InlineData(CreateClientStoreOutcome.ClientLimitReached, "client_limit_reached", ErrorCategory.PaymentRequired)]
    [InlineData(CreateClientStoreOutcome.SubscriptionInactive, "subscription_inactive", ErrorCategory.PaymentRequired)]
    [InlineData(CreateClientStoreOutcome.SubscriptionSuspended, "subscription_suspended", ErrorCategory.PaymentRequired)]
    [InlineData(CreateClientStoreOutcome.SubscriptionCancelled, "subscription_cancelled", ErrorCategory.PaymentRequired)]
    public async Task Create_FunctionalOutcome_MapsExpectedError(
        CreateClientStoreOutcome outcome,
        string expectedCode,
        ErrorCategory expectedCategory)
    {
        using var cancellationSource = new CancellationTokenSource();
        var store = new FakeClientStore { CreateOutcome = outcome };
        var handler = CreateCreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidCommand(),
            cancellationSource.Token);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(expectedCategory, result.Error.Category);
        Assert.Equal(cancellationSource.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task Create_Created_ForwardsTenantClockAndReturnsEmptyPacks()
    {
        using var cancellationSource = new CancellationTokenSource();
        var store = new FakeClientStore();
        var handler = CreateCreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidCommand(),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.UsablePacks);
        Assert.Equal(TrainerId, store.LastTrainerId);
        Assert.Equal(TrainerId, store.LastClient!.OwnerTrainerId);
        Assert.Equal(_clock.UtcNow, store.LastNow);
        Assert.Equal(cancellationSource.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task Create_SubscriptionMissing_ThrowsInvariantError()
    {
        var store = new FakeClientStore
        {
            CreateOutcome = CreateClientStoreOutcome.SubscriptionMissing
        };
        var handler = CreateCreateHandler(store, CreateTenant());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            ClientTestData.CreateValidCommand(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Get_QueryReturnsNull_ReturnsNotFound()
    {
        using var cancellationSource = new CancellationTokenSource();
        var queries = new FakeClientQueries();
        var handler = new GetClientHandler(CreateTenant(), queries);

        var result = await handler.HandleAsync(
            new GetClientQuery(ClientId),
            cancellationSource.Token);

        Assert.Equal("client_not_found", result.Error!.Code);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
        Assert.Equal(cancellationSource.Token, queries.LastCancellationToken);
    }

    [Fact]
    public async Task Get_EmptyId_ReturnsValidationWithoutCallingQueries()
    {
        var queries = new FakeClientQueries();
        var handler = new GetClientHandler(CreateTenant(), queries);

        var result = await handler.HandleAsync(
            new GetClientQuery(Guid.Empty),
            CancellationToken.None);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains(
            result.Error.ValidationErrors,
            error => error.Code == "client_id_required");
        Assert.Equal(0, queries.DetailsCalls);
    }

    [Fact]
    public async Task Get_Success_UsesClockDate()
    {
        using var cancellationSource = new CancellationTokenSource();
        var details = ClientTestData.CreateDetails(ClientId);
        var queries = new FakeClientQueries { DetailsResult = details };
        var handler = new GetClientHandler(CreateTenant(), queries);

        var result = await handler.HandleAsync(
            new GetClientQuery(ClientId),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Same(details, result.Value);
        Assert.Equal(cancellationSource.Token, queries.LastCancellationToken);
    }

    [Fact]
    public async Task Get_MissingTenant_DoesNotCallQueries()
    {
        var queries = new FakeClientQueries();
        var handler = new GetClientHandler(new StubTenantContext(), queries);

        var result = await handler.HandleAsync(
            new GetClientQuery(ClientId),
            CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, queries.DetailsCalls);
    }

    [Fact]
    public async Task List_InvalidQuery_DoesNotCallQueries()
    {
        var queries = new FakeClientQueries();
        var handler = new ListClientsHandler(
            new ListClientsQueryValidator(),
            CreateTenant(),
            queries);

        var result = await handler.HandleAsync(
            new ListClientsQuery(null, ClientActivityFilter.Active, 0, 50),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task List_MissingTenant_DoesNotCallQueries()
    {
        var queries = new FakeClientQueries();
        var handler = new ListClientsHandler(
            new ListClientsQueryValidator(),
            new StubTenantContext(),
            queries);

        var result = await handler.HandleAsync(
            new ListClientsQuery(null),
            CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task List_Success_NormalizesArgumentsAndPreservesResult()
    {
        using var cancellationSource = new CancellationTokenSource();
        var expectedPage = new PageResult<ClientSummaryDto>(
            Array.Empty<ClientSummaryDto>(),
            7);
        var queries = new FakeClientQueries { PageResult = expectedPage };
        var handler = new ListClientsHandler(
            new ListClientsQueryValidator(),
            CreateTenant(),
            queries);

        var result = await handler.HandleAsync(
            new ListClientsQuery("  ana  ", ClientActivityFilter.Archived, 2, 25),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Same(expectedPage, result.Value);
        Assert.Equal("ana", queries.LastSearch);
        Assert.Equal(ClientActivityFilter.Archived, queries.LastActivity);
        Assert.Equal(new PageRequest(2, 25), queries.LastPage);
        Assert.Equal(cancellationSource.Token, queries.LastCancellationToken);
    }

    [Fact]
    public async Task Update_ClientMissing_ReturnsNotFoundWithoutSaving()
    {
        var store = new FakeClientStore();
        var queries = new FakeClientQueries();
        var handler = CreateUpdateHandler(store, queries, CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidUpdateCommand(ClientId),
            CancellationToken.None);

        Assert.Equal("client_not_found", result.Error!.Code);
        Assert.Equal(0, store.SaveProfileCalls);
    }

    [Fact]
    public async Task Update_InvalidCommand_DoesNotLoadOrSaveClient()
    {
        var store = new FakeClientStore();
        var handler = CreateUpdateHandler(store, new FakeClientQueries(), CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidUpdateCommand(Guid.Empty),
            CancellationToken.None);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Equal(0, store.GetForUpdateCalls);
        Assert.Equal(0, store.SaveProfileCalls);
    }

    [Fact]
    public async Task Update_MissingTenant_DoesNotLoadOrSaveClient()
    {
        var store = new FakeClientStore();
        var handler = CreateUpdateHandler(store, new FakeClientQueries(), new StubTenantContext());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidUpdateCommand(ClientId),
            CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, store.GetForUpdateCalls);
        Assert.Equal(0, store.SaveProfileCalls);
    }

    [Theory]
    [InlineData(SaveClientProfileOutcome.DuplicateEmail, "client_email_already_exists")]
    [InlineData(SaveClientProfileOutcome.DuplicatePhone, "client_phone_already_exists")]
    public async Task Update_Duplicate_MapsConflict(
        SaveClientProfileOutcome outcome,
        string expectedCode)
    {
        var client = ClientTestData.CreateValidClient(TrainerId);
        var store = new FakeClientStore
        {
            ClientForUpdate = client,
            SaveOutcome = outcome
        };
        var queries = new FakeClientQueries();
        var handler = CreateUpdateHandler(store, queries, CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidUpdateCommand(client.Id),
            CancellationToken.None);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(ErrorCategory.Conflict, result.Error.Category);
        Assert.Equal(0, queries.UsablePacksCalls);
    }

    [Fact]
    public async Task Update_ArchivedClient_UpdatesProfileWithoutReactivating()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = ClientTestData.CreateValidClient(TrainerId, isActive: false);
        var packs = new List<UsableClientPackDto>
        {
            ClientTestData.CreatePackDetails()
        };
        var store = new FakeClientStore { ClientForUpdate = client };
        var queries = new FakeClientQueries { UsablePacksResult = packs };
        var handler = CreateUpdateHandler(store, queries, CreateTenant());

        var result = await handler.HandleAsync(
            ClientTestData.CreateValidUpdateCommand(client.Id),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.False(client.IsActive);
        Assert.Equal(1, store.SaveProfileCalls);
        Assert.Same(client, store.LastClient);
        Assert.Same(packs, result.Value.UsablePacks);
        Assert.Equal(cancellationSource.Token, queries.LastCancellationToken);
    }

    [Theory]
    [InlineData(ArchiveClientStoreOutcome.Archived)]
    [InlineData(ArchiveClientStoreOutcome.AlreadyArchived)]
    public async Task Archive_IdempotentOutcome_ReturnsSuccess(ArchiveClientStoreOutcome outcome)
    {
        using var cancellationSource = new CancellationTokenSource();
        var store = new FakeClientStore { ArchiveOutcome = outcome };
        var handler = new ArchiveClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ArchiveClientCommand(ClientId),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClientId, store.LastClientId);
        Assert.Equal(TrainerId, store.LastTrainerId);
        Assert.Equal(_clock.UtcNow, store.LastNow);
        Assert.Equal(cancellationSource.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task Archive_EmptyId_ReturnsValidationWithoutCallingStore()
    {
        var store = new FakeClientStore();
        var handler = new ArchiveClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ArchiveClientCommand(Guid.Empty),
            CancellationToken.None);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains(
            result.Error.ValidationErrors,
            error => error.Code == "client_id_required");
        Assert.Equal(0, store.ArchiveCalls);
    }

    [Fact]
    public async Task Archive_MissingTenant_DoesNotCallStore()
    {
        var store = new FakeClientStore();
        var handler = new ArchiveClientHandler(new StubTenantContext(), _clock, store);

        var result = await handler.HandleAsync(
            new ArchiveClientCommand(ClientId),
            CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, store.ArchiveCalls);
    }

    [Fact]
    public async Task Archive_NotFound_MapsExpectedError()
    {
        var store = new FakeClientStore
        {
            ArchiveOutcome = ArchiveClientStoreOutcome.NotFound
        };
        var handler = new ArchiveClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ArchiveClientCommand(ClientId),
            CancellationToken.None);

        Assert.Equal("client_not_found", result.Error!.Code);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
    }

    [Fact]
    public async Task Archive_SubscriptionMissing_ThrowsInvariantError()
    {
        var store = new FakeClientStore
        {
            ArchiveOutcome = ArchiveClientStoreOutcome.SubscriptionMissing
        };
        var handler = new ArchiveClientHandler(CreateTenant(), _clock, store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new ArchiveClientCommand(ClientId),
            CancellationToken.None));
    }

    [Theory]
    [InlineData(ReactivateClientStoreOutcome.SubscriptionInactive, "subscription_inactive")]
    [InlineData(ReactivateClientStoreOutcome.SubscriptionSuspended, "subscription_suspended")]
    [InlineData(ReactivateClientStoreOutcome.SubscriptionCancelled, "subscription_cancelled")]
    [InlineData(ReactivateClientStoreOutcome.ClientLimitReached, "client_limit_reached")]
    public async Task Reactivate_BlockedOutcome_MapsExpectedError(
        ReactivateClientStoreOutcome outcome,
        string expectedCode)
    {
        var store = new FakeClientStore { ReactivateOutcome = outcome };
        var handler = new ReactivateClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ReactivateClientCommand(ClientId),
            CancellationToken.None);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(ErrorCategory.PaymentRequired, result.Error.Category);
    }

    [Theory]
    [InlineData(ReactivateClientStoreOutcome.Reactivated)]
    [InlineData(ReactivateClientStoreOutcome.AlreadyActive)]
    public async Task Reactivate_IdempotentOutcome_ReturnsSuccess(ReactivateClientStoreOutcome outcome)
    {
        using var cancellationSource = new CancellationTokenSource();
        var store = new FakeClientStore { ReactivateOutcome = outcome };
        var handler = new ReactivateClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ReactivateClientCommand(ClientId),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClientId, store.LastClientId);
        Assert.Equal(TrainerId, store.LastTrainerId);
        Assert.Equal(_clock.UtcNow, store.LastNow);
        Assert.Equal(cancellationSource.Token, store.LastCancellationToken);
    }

    [Fact]
    public async Task Reactivate_EmptyId_ReturnsValidationWithoutCallingStore()
    {
        var store = new FakeClientStore();
        var handler = new ReactivateClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ReactivateClientCommand(Guid.Empty),
            CancellationToken.None);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains(
            result.Error.ValidationErrors,
            error => error.Code == "client_id_required");
        Assert.Equal(0, store.ReactivateCalls);
    }

    [Fact]
    public async Task Reactivate_MissingTenant_DoesNotCallStore()
    {
        var store = new FakeClientStore();
        var handler = new ReactivateClientHandler(new StubTenantContext(), _clock, store);

        var result = await handler.HandleAsync(
            new ReactivateClientCommand(ClientId),
            CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, store.ReactivateCalls);
    }

    [Fact]
    public async Task Reactivate_NotFound_MapsExpectedError()
    {
        var store = new FakeClientStore
        {
            ReactivateOutcome = ReactivateClientStoreOutcome.NotFound
        };
        var handler = new ReactivateClientHandler(CreateTenant(), _clock, store);

        var result = await handler.HandleAsync(
            new ReactivateClientCommand(ClientId),
            CancellationToken.None);

        Assert.Equal("client_not_found", result.Error!.Code);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
    }

    [Fact]
    public async Task Reactivate_SubscriptionMissing_ThrowsInvariantError()
    {
        var store = new FakeClientStore
        {
            ReactivateOutcome = ReactivateClientStoreOutcome.SubscriptionMissing
        };
        var handler = new ReactivateClientHandler(CreateTenant(), _clock, store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new ReactivateClientCommand(ClientId),
            CancellationToken.None));
    }

    private CreateClientHandler CreateCreateHandler(
        FakeClientStore store,
        StubTenantContext tenantContext)
    {
        return new CreateClientHandler(
            new CreateClientCommandValidator(_clock),
            tenantContext,
            _clock,
            store);
    }

    private UpdateClientHandler CreateUpdateHandler(
        FakeClientStore store,
        FakeClientQueries queries,
        StubTenantContext tenantContext)
    {
        return new UpdateClientHandler(
            new UpdateClientCommandValidator(_clock),
            tenantContext,
            _clock,
            store,
            queries);
    }

    private static StubTenantContext CreateTenant() => new() { TrainerId = TrainerId };
}
