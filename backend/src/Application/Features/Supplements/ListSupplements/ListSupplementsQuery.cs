namespace Application.Features.Supplements.ListSupplements;

/// <summary>Pesquisa paginada de suplementos visíveis ao personal trainer.</summary>
public sealed record ListSupplementsQuery(
    string? Search,
    SupplementActivityFilter Activity = SupplementActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
