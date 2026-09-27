using Application.Features.Sessions.Dtos;
using Domain.Entities.Sessions;

namespace Application.Features.Sessions;

/// <summary>Converte entidades Sessions em contratos da Application.</summary>
public static class SessionMappings
{
    /// <summary>Mapeia uma sessão sem expor o tenant.</summary>
    /// <param name="session">Sessão persistida.</param>
    /// <param name="clientName">Nome do cliente, lido pelo store na mesma transação.</param>
    public static SessionDto ToDto(this Session session, string clientName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        return new SessionDto(
            session.Id,
            session.ClientId,
            clientName,
            session.ClientSessionPackId,
            session.StartsAt,
            session.DurationMinutes,
            session.Location,
            session.SessionType,
            session.Notes,
            session.Status.Value,
            session.StatusChangedAt,
            session.CreatedAt,
            session.UpdatedAt
        );
    }
}
