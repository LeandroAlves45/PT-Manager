using Application.Features.Packs.PackTypes.Dtos;
using Application.Features.Packs.PackTypes.ListPackTypes;
using Application.Pagination;

namespace Application.Features.Packs.PackTypes.Abstractions;

/// <summary>Executa leituras projetadas de tipos de packs do tenant.</summary>
public interface IPackTypeQueries
{
    Task<PackTypeDto?> GetAsync(
        Guid trainerId,
        Guid packTypeId,
        CancellationToken cancellationToken
    );

    Task<PageResult<PackTypeDto>> ListAsync(
        Guid trainerId,
        string? search,
        PackTypeActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
