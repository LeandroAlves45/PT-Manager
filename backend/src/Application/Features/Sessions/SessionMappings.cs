using Application.Features.Sessions.Dtos;
using Domain.Entities.Sessions;

namespace Application.Features.Sessions;

/// <summary>Converte entidades Sessions em contratos da Application.</summary>
public static class SessionMappings
{
    /// <summary>Mapeia uma sessão sem expor o tenant.</summary>
    public static SessionDto ToDto(this Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionDto(
            session.Id,
            session.ClientId,
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
