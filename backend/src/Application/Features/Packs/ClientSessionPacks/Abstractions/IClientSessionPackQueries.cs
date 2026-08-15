using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;
using Application.Pagination;

namespace Application.Features.Packs.ClientSessionPacks.Abstractions;

/// <summary>Consulta packs atribuídos dentro do tenant efectivo.</summary>
public interface IClientSessionPackQueries
{
    Task<ClientSessionPackDto?> GetAsync(
        Guid trainerId,
        Guid packId,
        CancellationToken cancellationToken
    );

    Task<PageResult<ClientSessionPackDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        ClientSessionPackActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ClientSessionPackDto>> ListUsableAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken
    );
}
