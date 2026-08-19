using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.ListSupplementAssignments;
using Application.Pagination;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Consulta atribuições sem expor entidades ou notas internas do catálogo.</summary>
public interface IClientSupplementAssignmentQueries
{
    Task<ClientSupplementAssignmentDto?> GetAsync(
        Guid trainerId,
        Guid assignmentId,
        CancellationToken cancellationToken
    );

    Task<PageResult<ClientSupplementAssignmentDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        SupplementAssignmentActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );

    Task<MySupplementAssignmentDto?> GetMyAsync(
        Guid trainerId,
        Guid userId,
        Guid assignmentId,
        CancellationToken cancellationToken
    );

    Task<PageResult<MySupplementAssignmentDto>> ListMyActiveAsync(
        Guid trainerId,
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
