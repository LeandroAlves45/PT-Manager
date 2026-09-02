using Application.Pagination;

namespace Api.Contracts.Common;

/// <summary>Envelope estável das listagens paginadas. Serializa como
/// items, total_count, page_number, page_size.
/// </summary>
public sealed record PagedResponse<TItem>(
    IReadOnlyList<TItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    /// <summary>Projeta um PageResult da Application no envelope da API.</summary>
    public static PagedResponse<TItem> From<TSource>(
        PageResult<TSource> page,
        int pageNumber,
        int pageSize,
        Func<TSource, TItem> projection)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(projection);

        return new PagedResponse<TItem>(
            page.Items.Select(projection).ToArray(),
            page.TotalCount,
            pageNumber,
            pageSize
        );
    }
}
