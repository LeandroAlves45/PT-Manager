namespace Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;

/// <summary>Lista packs atribuídos no tenant.</summary>
public sealed record ListClientSessionPacksQuery(
    Guid? ClientId,
    ClientSessionPackActivityFilter Activity = ClientSessionPackActivityFilter.Usable,
    int PageNumber = 1,
    int PageSize = 50
);
