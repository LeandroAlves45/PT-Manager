using Domain.Exceptions;
namespace Domain.Entities.Sessions;

/// <summary>
/// Sessão de treino agendada ou realizada, entre o Personal Trainer e o Cliente.
/// </summary>
public class Session
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly SessionDate { get; private set; }
    public TimeOnly? SessionTime { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string? SessionType { get; private set; }
    public string? Notes { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Session() { }

    /// <summary>Agenda uma nova sessão de treino.</summary>
    public Session(
        Guid ownerTrainerId,
        Guid clientId,
        DateOnly sessionDate,
        TimeOnly? sessionTime,
        int? durationMinutes,
        string? sessionType,
        string? notes,
        DateTime now
    )
    {
        if (sessionType != null && sessionType.Length > 50)
            throw new DomainException("Session type cannot exceed 50 characters.");
        if (durationMinutes.HasValue && durationMinutes.Value <= 0)
            throw new DomainException("Duration must be positive");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        SessionDate = sessionDate;
        SessionTime = sessionTime;
        DurationMinutes = durationMinutes;
        SessionType = sessionType;
        Notes = notes;
        IsCompleted = false;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Reagenda a sessão de treino (novo dia/hora), apenas se ainda não realizada.</summary>
    public void Reschedule(DateOnly newDate, TimeOnly? newTime, DateTime now)
    {
        if (IsCompleted)
            throw new DomainException("Cannot reschedule a completed session.");

        SessionDate = newDate;
        SessionTime = newTime;
        UpdatedAt = now;
    }

    /// <summary>Marca a sessão como concluída (idempotente).</summary>
    public void MarkAsCompleted(DateTime now)
    {
        if (!IsCompleted)
        {
            IsCompleted = true;
            UpdatedAt = now;
        }
    }

    /// <summary>Soft delete da sessão, marcando-a como excluída.</summary>
    public void SoftDelete(DateTime now)
    {
        if (IsCompleted)
            throw new DomainException("Cannot delete a completed session.");
        IsDeleted = true;
        UpdatedAt = now;
    }
}
