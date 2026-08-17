namespace Application.Features.Sessions.RescheduleSession;

/// <summary>Reagenda uma sessão Scheduled.</summary>
public sealed record RescheduleSessionCommand(
    Guid SessionId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location
);
