namespace Application.Features.Sessions.Abstractions;

/// <summary>Persiste mutações tenant-safe e transacionais de sessões.</summary>
public interface ISessionStore
{
    Task<SessionStoreResult> CreateAsync(
        Guid trainerId,
        Guid clientId,
        Guid? packId,
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        string? sessionType,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<SessionStoreResult> RescheduleAsync(
        Guid trainerId,
        Guid sessionId,
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<SessionStoreResult> ChangePackAsync(
        Guid trainerId,
        Guid sessionId,
        Guid? packId,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<SessionStoreResult> TransitionAsync(
        Guid trainerId,
        Guid sessionId,
        SessionTransition transition,
        DateTime now,
        CancellationToken cancellationToken
    );
}
