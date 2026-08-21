using Application.Common.Abstractions;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.CreateClient;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.ListClients;
using Application.Features.Clients.UpdateClient;
using Application.Pagination;
using Domain.Entities.Clients;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Clients;

/// <summary>Relógio determinístico dos testes de Clients.</summary>
internal sealed class StubClock : IClock
{
    public DateTime UtcNow { get; init; }
}

/// <summary>Contexto tenant configurável sem dependências externas.</summary>
internal sealed class StubTenantContext : ITenantContext
{
    public Guid? TrainerId { get; init; }

    // UserId tem de ser um Guid válido por default: ActorAuthorization.RequireTrainer
    // passou a exigi-lo (UnauthenticatedUser se ausente). Testes que queiram simular
    // ausência de identidade continuam a poder fazê-lo com "UserId = null".
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public string? Role { get; init; } = "trainer";
    public TenantOrigin Origin { get; init; } = TenantOrigin.Http;
    public bool IsAdministrative { get; init; }
}

/// <summary>Fake observável da porta escrita.</summary>
internal sealed class FakeClientStore : IClientStore
{
    public CreateClientStoreOutcome CreateOutcome { get; set; } = CreateClientStoreOutcome.Created;
    public Client? ClientForUpdate { get; set; }
    public SaveClientProfileOutcome SaveOutcome { get; set; } = SaveClientProfileOutcome.Updated;
    public ArchiveClientStoreOutcome ArchiveOutcome { get; set; } = ArchiveClientStoreOutcome.Archived;
    public ReactivateClientStoreOutcome ReactivateOutcome { get; set; } = ReactivateClientStoreOutcome.Reactivated;

    public int CreateCalls { get; private set; }
    public int GetForUpdateCalls { get; private set; }
    public int SaveProfileCalls { get; private set; }
    public int ArchiveCalls { get; private set; }
    public int ReactivateCalls { get; private set; }

    public Client? LastClient { get; private set; }
    public Guid LastClientId { get; private set; }
    public Guid LastTrainerId { get; private set; }
    public DateTime LastNow { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<CreateClientStoreOutcome> CreateWithSubscriptionSlotAsync(
        Client client,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        CreateCalls++;
        LastClient = client;
        LastClientId = client.Id;
        LastTrainerId = trainerId;
        LastNow = now;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(CreateOutcome);
    }

    public Task<Client?> GetForUpdateAsync(Guid clientId, CancellationToken cancellationToken)
    {
        GetForUpdateCalls++;
        LastClientId = clientId;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(ClientForUpdate);
    }

    public Task<SaveClientProfileOutcome> SaveProfileAsync(
        Client client,
        CancellationToken cancellationToken
    )
    {
        SaveProfileCalls++;
        LastClient = client;
        LastClientId = client.Id;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(SaveOutcome);
    }

    public Task<ArchiveClientStoreOutcome> ArchiveAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        ArchiveCalls++;
        LastClientId = clientId;
        LastTrainerId = trainerId;
        LastNow = now;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(ArchiveOutcome);
    }

    public Task<ReactivateClientStoreOutcome> ReactivateAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        ReactivateCalls++;
        LastClientId = clientId;
        LastTrainerId = trainerId;
        LastNow = now;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(ReactivateOutcome);
    }
}

/// <summary>Fake observável da porta de leitura.</summary>
internal sealed class FakeClientQueries : IClientQueries
{
    public ClientDetailsDto? DetailsResult { get; set; }
    public PageResult<ClientSummaryDto> PageResult { get; set; } = new
        PageResult<ClientSummaryDto>(Array.Empty<ClientSummaryDto>(), 0);
    public IReadOnlyList<UsableClientPackDto> UsablePacksResult { get; set; }
        = Array.Empty<UsableClientPackDto>();

    public int DetailsCalls { get; private set; }
    public int ListCalls { get; private set; }
    public int UsablePacksCalls { get; private set; }
    public Guid LastClientId { get; private set; }
    public string? LastSearch { get; private set; }
    public ClientActivityFilter LastActivity { get; private set; }
    public PageRequest? LastPage { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<ClientDetailsDto?> GetDetailsAsync(
        Guid clientId,
        CancellationToken cancellationToken
    )
    {
        DetailsCalls++;
        LastClientId = clientId;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(DetailsResult);
    }

    public Task<PageResult<ClientSummaryDto>> ListAsync(
        string? search,
        ClientActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        ListCalls++;
        LastSearch = search;
        LastActivity = activity;
        LastPage = page;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(PageResult);
    }

    public Task<IReadOnlyList<UsableClientPackDto>> ListUsablePacksAsync(
        Guid clientId,
        CancellationToken cancellationToken
    )
    {
        UsablePacksCalls++;
        LastClientId = clientId;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(UsablePacksResult);
    }
}

/// <summary>Cria dados válidos e determinísticos para os testes.</summary>
internal static class ClientTestData
{
    internal static readonly DateTime NowUtc = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    internal static CreateClientCommand CreateValidCommand()
    {
        return new CreateClientCommand(
            Name: "John Doe",
            ContactEmail: "john@example.com",
            Phone: "+351 912 345 678",
            BirthDate: new DateOnly(1995, 1, 1),
            Sex: "female",
            Objective: "Strength",
            Notes: null,
            EmergencyContactName: "Jane Doe",
            EmergencyContactPhone: "+351 987 654 321"
        );
    }

    internal static UpdateClientCommand CreateValidUpdateCommand(Guid clientId)
    {
        return new UpdateClientCommand(
            ClientId: clientId,
            Name: "John Doe Updated",
            ContactEmail: "john.updated@example.com",
            Phone: "+351 912 345 679",
            BirthDate: new DateOnly(1995, 1, 1),
            Sex: "female",
            Objective: "Hypertrophy",
            Notes: "Updated notes",
            EmergencyContactName: "Jane Doe",
            EmergencyContactPhone: "+351 987 654 322"
        );
    }

    internal static Client CreateValidClient(Guid trainerId, bool isActive = true)
    {
        var client = new Client(
            trainerId,
            "John Doe",
            "john@example.com",
            "+351 912 345 678",
            BirthDate.Create(new DateOnly(1995, 1, 1), DateOnly.FromDateTime(NowUtc)),
            BiologicalSex.Female,
            "Strength",
            null,
            null,
            null,
            NowUtc
        );

        if (!isActive)
            client.Deactivate(NowUtc.AddMinutes(1));
        return client;
    }

    internal static UsableClientPackDto CreatePackDetails()
    {
        return new UsableClientPackDto(
            Id: Guid.NewGuid(),
            PackTypeId: Guid.NewGuid(),
            Name: "10 Sessions",
            SessionsTotal: 10,
            SessionsRemaining: 4,
            PriceCents: 10000,
            Currency: "EUR",
            PurchaseDate: new DateOnly(2026, 7, 1),
            ExpectedEndDate: new DateOnly(2026, 9, 1),
            CreatedAt: NowUtc);
    }

    internal static ClientDetailsDto CreateDetails(Guid clientId)
    {
        return new ClientDetailsDto(
            Id: clientId,
            UserId: null,
            Name: "John Doe",
            ContactEmail: "john@example.com",
            Phone: "+351 912 345 678",
            BirthDate: new DateOnly(1995, 1, 1),
            Sex: "female",
            Objective: "Strength",
            Notes: null,
            EmergencyContactName: null,
            EmergencyContactPhone: null,
            AvatarUrl: null,
            IsActive: true,
            UsablePacks: Array.Empty<UsableClientPackDto>(),
            CreatedAt: NowUtc,
            UpdatedAt: NowUtc
        );
    }
}
