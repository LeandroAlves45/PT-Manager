using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Sessions;

/// <summary>Sessão de treino agendada entre o Personal Trainer e o Cliente.</summary>
public sealed class Session
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? ClientSessionPackId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
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

    /// <summary>Cria uma sessão Scheduled e normaliza o início para UTC.</summary>
    public Session(
        Guid ownerTrainerId,
        Guid clientId,
        Guid? clientSessionPackId,
        DateTimeOffset startsAt,
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

        ValidateSchedule(durationMinutes, location, sessionType);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        ClientSessionPackId = clientSessionPackId;
        StartsAt = startsAt.ToUniversalTime();
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

    /// <summary>Altera data, duração e localização de uma sessão Scheduled.</summary>
    public void Reschedule(
        DateTimeOffset startsAt,
        int durationMinutes,
        string? location,
        DateTime now)
    {
        EnsureScheduled();
        ValidateSchedule(durationMinutes, location, SessionType);

        var normalizedStartsAt = startsAt.ToUniversalTime();
        var normalizedLocation = NormalizeOptional(location);
        if (StartsAt == normalizedStartsAt &&
            DurationMinutes == durationMinutes &&
            Location == normalizedLocation)
            return;

        StartsAt = normalizedStartsAt;
        DurationMinutes = durationMinutes;
        Location = normalizedLocation;
        UpdatedAt = now;
    }

    /// <summary>Troca ou remove o pack de uma sessão ainda Scheduled.</summary>
    public void ChangePack(Guid? clientSessionPackId, DateTime now)
    {
        EnsureScheduled();
        if (clientSessionPackId.HasValue && clientSessionPackId.Value == Guid.Empty)
            throw new DomainException("Client session pack ID cannot be empty");
        if (ClientSessionPackId == clientSessionPackId)
            return;

        ClientSessionPackId = clientSessionPackId;
        UpdatedAt = now;
    }

    /// <summary>Marca a sessão como concluída de forma idempotente.</summary>
    public void Complete(DateTime now) =>
        SetTerminalStatus(SessionStatus.Completed, now);

    /// <summary>Regista cancelamento solicitado pelo cliente.</summary>
    public void CancelByClient(DateTime now) =>
        SetTerminalStatus(SessionStatus.CancelledByClient, now);

    /// <summary>Regista cancelamento solicitado pelo personal trainer.</summary>
    public void CancelByTrainer(DateTime now) =>
        SetTerminalStatus(SessionStatus.CancelledByTrainer, now);

    /// <summary>Regista que o cliente não compareceu à sessão.</summary>
    public void MarkNoShow(DateTime now) =>
        SetTerminalStatus(SessionStatus.NoShow, now);

    /// <summary>Repõe uma sessão terminal em Scheduled para correção pelo personal trainer.</summary>
    public void Restore(DateTime now)
    {
        EnsureNotDeleted();
        if (Status == SessionStatus.Scheduled)
            return;

        Status = SessionStatus.Scheduled;
        StatusChangedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Soft delete da sessão, marcando-a como excluída.</summary>
    public void SoftDelete(DateTime now)
    {
        EnsureScheduled();
        IsDeleted = true;
        UpdatedAt = now;
    }

    private void SetTerminalStatus(SessionStatus status, DateTime now)
    {
        EnsureNotDeleted();
        if (Status == status)
            return;

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
        int durationMinutes,
        string? location,
        string? sessionType)
    {
        if (durationMinutes <= 0)
            throw new DomainException("Duration must be greater than zero.");
        if (NormalizeOptional(location) is { Length: > 255 })
            throw new DomainException("Location cannot exceed 255 characters.");
        if (NormalizeOptional(sessionType) is { Length: > 50 })
            throw new DomainException("Session type cannot exceed 50 characters.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
