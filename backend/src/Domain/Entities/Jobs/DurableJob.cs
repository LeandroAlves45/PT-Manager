using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Jobs;

/// <summary>
/// Job durável persistido em Postgres, processado pelo dispatcher interno
/// (ativado por QStash). Entrega at-least-once: o handler do job tem de ser
/// idempotente, apoiado em IdempotencyKey.
/// </summary>
public class DurableJob
{
    public Guid Id { get; private set; }
    public Guid? TrainerId { get; private set; }
    public string JobType { get; private set; } = null!;
    public int JobVersion { get; private set; }
    public string Payload { get; private set; } = null!;
    public JobStatus Status { get; private set; } = null!;
    public DateTime ScheduledAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    /// <summary>Chave de idempotência única — o handler usa-a para não repetir efeitos.</summary>
    public string IdempotencyKey { get; private set; } = null!;
    /// <summary>Correlation ID propagado nos logs e chamadas externas.</summary>
    public Guid CorrelationId { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private DurableJob() { }

    /// <summary>Cria um job pendente, agendado para scheduledAt.</summary>
    public DurableJob(
        Guid? trainerId,
        string jobType,
        int jobVersion,
        string payloadJson,
        string idempotencyKey,
        Guid correlationId,
        DateTime scheduledAt,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(jobType))
            throw new DomainException("Job type cannot be empty.");
        if (jobType.Length > 100)
            throw new DomainException("Job type cannot exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("Payload is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency key is required.");
        if (idempotencyKey.Length > 255)
            throw new DomainException("Idempotency key cannot exceed 255 characters.");
        if (jobVersion <= 0)
            throw new DomainException("Job version must be positive.");
        if (correlationId == Guid.Empty)
            throw new DomainException("Correlation ID is required.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        JobType = jobType;
        JobVersion = jobVersion;
        Payload = payloadJson;
        Status = JobStatus.Pending;
        ScheduledAt = scheduledAt;
        Attempts = 0;
        IdempotencyKey = idempotencyKey;
        CorrelationId = correlationId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// True se o dispatcher pode reclamar este job: pendente ou vencido, OU
    /// em processamento com lease expirado. (worker morreu)
    /// </summary>
    public bool IsClaimable(DateTime now) =>
        (Status == JobStatus.Pending && ScheduledAt <= now) ||
        (Status == JobStatus.Processing && LeaseExpiresAt.HasValue && LeaseExpiresAt.Value <= now);

    /// <summary>
    /// Reclama o job para execução: passa a Processing, aplica lease e conta a tentativa.
    /// </summary>
    public void Claim(TimeSpan leaseDuration, DateTime now)
    {
        if (!IsClaimable(now))
            throw new DomainException("Job is not claimable for processing.");
        if (leaseDuration <= TimeSpan.Zero)
            throw new DomainException("Lease duration must be positive.");

        Status = JobStatus.Processing;
        LeaseExpiresAt = now.Add(leaseDuration);
        Attempts += 1;
        UpdatedAt = now;
    }

    /// <summary>Conclui o job com sucesso.</summary>
    public void MarkCompleted(DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a processing job can be marked as completed.");

        Status = JobStatus.Completed;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Regista o resultado falhado de uma tentativa.</summary>
    public void RecordFailure(string sanitizedError, DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a processing job can record a failure.");

        LastError = sanitizedError;
        LeaseExpiresAt = null;
        Status = JobStatus.Failed;
        UpdatedAt = now;
    }

    /// <summary>Agenda nova tentativa depois de uma falha transitória.</summary>
    public void ScheduleRetry(DateTime nextAttemptAt, DateTime now)
    {
        if (Status != JobStatus.Failed)
            throw new DomainException("Only a failed job can be scheduled for retry.");
        if (nextAttemptAt <= now)
            throw new DomainException("Next attempt time must be in the future.");

        Status = JobStatus.Pending;
        NextAttemptAt = nextAttemptAt;
        ScheduledAt = nextAttemptAt;
        UpdatedAt = now;
    }

    /// <summary>Move uma falha permanente ou esgotada para dead letter.</summary>
    public void MoveToDeadLetter(DateTime now)
    {
        if (Status != JobStatus.Failed)
            throw new DomainException("Only a failed job can be moved to dead letter.");

        Status = JobStatus.DeadLetter;
        UpdatedAt = now;
    }
}
