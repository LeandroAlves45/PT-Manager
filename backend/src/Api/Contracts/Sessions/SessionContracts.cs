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
/// <remarks>
/// Chama-se <c>TrainingSessionResponse</c> e não <c>SessionResponse</c> porque o OpenAPI
/// identifica os schemas pelo nome curto do tipo: com dois <c>SessionResponse</c> (este e o da
/// autenticação) o documento fundia-os num só e o frontend recebia os campos do token aqui.
/// O JSON serializado é o mesmo; só o nome do schema muda.
/// </remarks>
public sealed record TrainingSessionResponse(
    Guid Id,
    Guid ClientId,
    string ClientName,
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
    public static TrainingSessionResponse From(SessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new(
            session.Id,
            session.ClientId,
            session.ClientName,
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
