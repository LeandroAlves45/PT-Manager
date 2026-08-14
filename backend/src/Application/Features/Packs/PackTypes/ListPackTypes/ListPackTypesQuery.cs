namespace Application.Features.Packs.PackTypes.ListPackTypes;

/// <summary>Lista tipos de pack privados do tenant.</summary>
public sealed record ListPackTypesQuery(
    string? Search,
    PackTypeActivityFilter Activity = PackTypeActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
