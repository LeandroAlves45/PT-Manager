using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Sessions;

/// <summary>
/// Sessão de treino agendada ou realizada, entre o Personal Trainer e o Cliente.
/// </summary>
public class Session
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? ClientSessionPackId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Location { get; private set; }
    public string? SessionType { get; private set; }
    public string? Notes { get; private set; }
    public SessionStatus Status { get; private set; } = null!;
    public DateTime StatusChangedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Session() { }

    /// <summary>Agenda uma nova sessão de treino.</summary>
    public Session(
        Guid ownerTrainerId,
        Guid clientId,
        Guid? clientSessionPackId,
        DateTime startsAt,
        int durationMinutes,
        string? location,
        string? sessionType,
        string? notes,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Owner trainer ID and client ID are required.");
        if (clientSessionPackId.HasValue && clientSessionPackId.Value == Guid.Empty)
            throw new DomainException("Client session pack ID cannot be empty");

        ValidateSchedule(startsAt, durationMinutes, location, sessionType);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        ClientSessionPackId = clientSessionPackId;
        StartsAt = EnsureUtc(startsAt);
        DurationMinutes = durationMinutes;
        Location = NormalizeOptional(location);
        SessionType = NormalizeOptional(sessionType);
        Notes = NormalizeOptional(notes);
        Status = SessionStatus.Scheduled;
        StatusChangedAt = now;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Reagenda a sessão de treino (novo dia/hora), apenas se ainda não realizada.</summary>
    public void Reschedule(
        DateTime startsAt,
        int durationMinutes,
        string? location,
        DateTime now)
    {
        EnsureScheduled();
        ValidateSchedule(startsAt, durationMinutes, location, SessionType);

        StartsAt = EnsureUtc(startsAt);
        DurationMinutes = durationMinutes;
        Location = NormalizeOptional(location);
        UpdatedAt = now;
    }

    /// <summary>Marca a sessão como concluída (idempotente).</summary>
    public void Complete(DateTime now)
    {
        EnsureNotDeleted();
        if (Status == SessionStatus.Completed)
            return;

        EnsureScheduled();
        SetTerminalStatus(SessionStatus.Completed, now);
    }

    /// <summary>Regista cancelamento solicitado pelo cliente.</summary>
    public void CancelByClient(DateTime now) => SetTerminalStatus(SessionStatus.CancelledByClient, now);

    /// <summary>Regista cancelamento solicitado pelo personal trainer.</summary>
    public void CancelByTrainer(DateTime now) => SetTerminalStatus(SessionStatus.CancelledByTrainer, now);

    /// <summary>Regista que o cliente não compareceu à sessão.</summary>
    public void MarkNoShow(DateTime now) => SetTerminalStatus(SessionStatus.NoShow, now);

    /// <summary>Soft delete da sessão, marcando-a como excluída.</summary>
    public void SoftDelete(DateTime now)
    {
        EnsureScheduled();
        IsDeleted = true;
        UpdatedAt = now;
    }

    private void SetTerminalStatus(SessionStatus status, DateTime now)
    {
        EnsureScheduled();
        Status = status;
        StatusChangedAt = now;
        UpdatedAt = now;
    }

    private void EnsureScheduled()
    {
        EnsureNotDeleted();
        if (Status != SessionStatus.Scheduled)
            throw new DomainException("Only a scheduled session can change state.");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted session.");
    }

    private void ValidateSchedule(
        DateTime startsAt,
        int durationMinutes,
        string? location,
        string? sessionType)
    {
        if (startsAt.Kind == DateTimeKind.Unspecified)
            throw new DomainException("Session start must include timezone information.");
        if (durationMinutes <= 0)
            throw new DomainException("Duration must be greater than zero.");
        if (NormalizeOptional(location) is { Length: > 255 })
            throw new DomainException("Location cannot exceed 255 characters.");
        if (NormalizeOptional(sessionType) is { Length: > 50 })
            throw new DomainException("Session type cannot exceed 50 characters.");
    }

    private static DateTime EnsureUtc(DateTime value) => value.ToUniversalTime();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
