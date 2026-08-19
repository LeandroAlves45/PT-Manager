namespace Application.Features.Supplements.ListGlobalSupplements;

/// <summary>Pesquisa paginada do catálogo global de suplementos.</summary>
public sealed record ListGlobalSupplementsQuery(
    string? Search,
    GlobalSupplementActivityFilter Activity = GlobalSupplementActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
