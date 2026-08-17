using Application.Features.Sessions.Dtos;
using Application.Features.Sessions.ListSessions;
using Application.Pagination;

namespace Application.Features.Sessions.Abstractions;

/// <summary>Consulta as sessões do tenant efetivo sem tracking.</summary>
public interface ISessionQueries
{
    Task<SessionDto?> GetAsync(
        Guid trainerId,
        Guid sessionId,
        CancellationToken cancellationToken
    );

    Task<PageResult<SessionDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        SessionStatusFilter? status,
        DateTimeOffset? startsFrom,
        DateTimeOffset? startsBefore,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
