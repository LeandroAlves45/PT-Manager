using Application.Features.Clients.Dtos;
using Application.Features.Clients.ListClients;
using Application.Pagination;

namespace Application.Features.Clients.Abstractions;

/// <summary>
/// Porta de leitura projetada da feature Clients.
/// A implementação respeita o tenant efectivo através do DBContext filtrado.
/// </summary>
public interface IClientQueries
{
    /// <summary>Obtém detalhe e packs utilizáveis ou null.</summary>
    Task<ClientDetailsDto?> GetDetailsAsync(
        Guid clientId,
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>Lista clientes com filtro e ordem determinística.</summary>
    Task<PageResult<ClientSummaryDto>> ListAsync(
        string? search,
        ClientActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken = default);

    /// <summary>Obtém todos os packs utilizáveis já ordenados.</summary>
    Task<IReadOnlyList<UsableClientPackDto>> ListUsablePacksAsync(
        Guid clientId,
        DateOnly today,
        CancellationToken cancellationToken = default);
}
