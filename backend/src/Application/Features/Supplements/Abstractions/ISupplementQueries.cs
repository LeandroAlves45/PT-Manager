using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.ListSupplements;
using Application.Pagination;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Consulta suplementos globais ativos e privados do tenant.</summary>
public interface ISupplementQueries
{
    Task<SupplementDto?> GetAsync(Guid trainerId, Guid supplementId, CancellationToken cancellationToken);

    Task<PageResult<SupplementDto>> ListAsync(
        Guid trainerId,
        string? search,
        SupplementActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
