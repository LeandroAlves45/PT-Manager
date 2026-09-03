using Application.Features.Sessions.Dtos;

namespace Api.Contracts.Sessions;

/// <summary>Agenda uma sessão, opcionalmente consumindo um pack.</summary>
public sealed record CreateSessionRequest(
    Guid ClientId,
    Guid? ClientSessionPackId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? SessionType,
    string? Notes);

/// <summary>Move uma sessão agendada para outro instante.</summary>
public sealed record RescheduleSessionRequest(
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location);

/// <summary>Associa a sessão a outro pack, ou a nenhum quando nulo.</summary>
public sealed record ChangeSessionPackRequest(Guid? ClientSessionPackId);

/// <summary>Sessão agendada e o seu estado atual.</summary>
public sealed record SessionResponse(
    Guid Id,
    Guid ClientId,
    Guid? ClientSessionPackId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? SessionType,
    string? Notes,
    string Status,
    DateTime StatusChangedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application.</summary>
    public static SessionResponse From(SessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new(
            session.Id,
            session.ClientId,
            session.ClientSessionPackId,
            session.StartsAt,
            session.DurationMinutes,
            session.Location,
            session.SessionType,
            session.Notes,
            session.Status,
            session.StatusChangedAt,
            session.CreatedAt,
            session.UpdatedAt);
    }
}
