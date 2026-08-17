namespace Application.Features.Sessions.CreateSession;

/// <summary>Agenda uma sessão para um cliente do tenant efetivo.</summary>
public sealed record CreateSessionCommand(
    Guid ClientId,
    Guid? ClientSessionPackId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? SessionType,
    string? Notes
);
