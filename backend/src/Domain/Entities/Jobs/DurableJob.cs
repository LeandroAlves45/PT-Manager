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
    /// <summary>
    /// Token opaco desta execução de claim. Null quando o job não está reclamado.
    /// </summary>
    public Guid? LeaseOwnerId { get; private set; }
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
    public bool IsClaimable(DateTime now)
    {
        var elegibleAt = NextAttemptAt ?? ScheduledAt;

        var pendingAndDue = Status == JobStatus.Pending && elegibleAt <= now;

        // Um job em Processing com lease expirado é um job abandonado. Sem esta
        // segunda condição, a morte de um worker prende o job para sempre.
        var abandoned = Status == JobStatus.Processing
            && LeaseExpiresAt.HasValue
            && LeaseExpiresAt.Value <= now;

        return pendingAndDue || abandoned;
    }

    /// <summary>
    /// Reclama o job para execução: passa a Processing, regista o owner do lease,
    /// aplica a expiração e conta a tentativa.
    /// </summary>
    public void Claim(Guid leaseOwnerId, TimeSpan leaseDuration, DateTime now)
    {
        if (leaseOwnerId == Guid.Empty)
            throw new DomainException("Lease owner is required.");
        if (!IsClaimable(now))
            throw new DomainException("Job is not claimable for processing.");
        if (leaseDuration <= TimeSpan.Zero)
            throw new DomainException("Lease duration must be positive.");

        Status = JobStatus.Processing;
        LeaseOwnerId = leaseOwnerId;
        LeaseExpiresAt = now.Add(leaseDuration);
        Attempts += 1;
        UpdatedAt = now;
    }

    /// <summary>Conclui o job com sucesso. Só o owner do lease pode concluir.</summary>
    public void MarkCompleted(Guid leaseOwnerId, DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a processing job can be marked as completed.");
        if (LeaseOwnerId != leaseOwnerId)
            throw new DomainException("Only the current lease owner can mark the job as completed.");
        if (!LeaseExpiresAt.HasValue || LeaseExpiresAt.Value <= now)
            throw new DomainException("An expired lease cannot mark the job as completed.");

        Status = JobStatus.Completed;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Regista o resultado falhado de uma tentativa. Só o owner do lease.</summary>
    public void RecordFailure(Guid leaseOwnerId, string sanitizedError, DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a processing job can record a failure.");
        if (LeaseOwnerId != leaseOwnerId)
            throw new DomainException("Only the current lease owner can record a failure.");
        if (!LeaseExpiresAt.HasValue || LeaseExpiresAt.Value <= now)
            throw new DomainException("An expired lease cannot record a failure.");

        Status = JobStatus.Failed;
        LastError = sanitizedError;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
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
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Move uma falha permanente ou esgotada para dead letter.</summary>
    public void MoveToDeadLetter(DateTime now)
    {
        if (Status != JobStatus.Failed)
            throw new DomainException("Only a failed job can be moved to dead letter.");

        Status = JobStatus.DeadLetter;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Estende o lease de um job em execução. Só o owner atual pode renovar.
    /// </summary>
    public void RenewLease(Guid leaseOwnerId, TimeSpan leaseDuration, DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a processing job can renew its lease.");
        if (LeaseOwnerId != leaseOwnerId)
            throw new DomainException("Only the current lease owner can renew the lease.");
        if (leaseDuration <= TimeSpan.Zero)
            throw new DomainException("Lease duration must be positive.");

        if (!LeaseExpiresAt.HasValue || LeaseExpiresAt.Value <= now)
            throw new DomainException("An expired lease cannot be renewed.");

        LeaseExpiresAt = now.Add(leaseDuration);
        UpdatedAt = now;
    }
}
