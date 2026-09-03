using Microsoft.AspNetCore.Mvc;

namespace Api.Contracts.Common;

/// <summary>
/// Parâmetros de paginação partilhados por todas as listagens da Api.
/// </summary>
public sealed record PageParameters
{
    public const int DefaultPageNumber = 1;

    public const int DefaultPageSize = 50;

    [FromQuery(Name = "page_number")]
    public int PageNumber { get; init; }

    [FromQuery(Name = "page_size")]
    public int PageSize { get; init; }

    public int EffectivePageNumber =>
        PageNumber <= 0 ? DefaultPageNumber : PageNumber;

    public int EffectivePageSize =>
        PageSize <= 0 ? DefaultPageSize : PageSize;
}
