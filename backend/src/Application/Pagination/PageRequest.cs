namespace Application.Pagination;

/// <summary>
/// Parâmetros normalizados de uma listagem paginada.
/// </summary>
public sealed record PageRequest(int PageNumber = 1, int PageSize = 50);
