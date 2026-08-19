using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.ListGlobalSupplements;
using Application.Pagination;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Consulta exclusivamente suplementos globais para administração.</summary>
public interface IGlobalSupplementQueries
{
    Task<GlobalSupplementDto?> GetAsync(Guid supplementId, CancellationToken cancellationToken);

    Task<PageResult<GlobalSupplementDto>> ListAsync(
        string? search,
        GlobalSupplementActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
