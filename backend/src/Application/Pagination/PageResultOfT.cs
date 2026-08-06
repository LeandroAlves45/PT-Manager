namespace Application.Pagination;

/// <summary>
/// Resultado paginado independente do transporte HTTP.
/// </summary>
public sealed record PageResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount);
