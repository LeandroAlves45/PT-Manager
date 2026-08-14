using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Jobs;

/// <summary>
/// Mensagem de outbox transacional: escrita na mesma transação da alteração
/// de dominio que a originou (ex: webhook Stripe processado -> email de confirmação por enviar).
/// O dispatcher entrega de forma idempotente.
/// </summary>
/// <remarks>
/// GARANTIA: at-least-once. Se o processo terminar depois de o fornecedor
/// externo aceitar o efeito e antes do commit local, o efeito é repetido. Onde o
/// fornecedor suporta chaves de idempotência, IdempotencyKey evita a duplicação;
/// onde não suporta, a duplicação é aceite e documentada. Exactly-once não é
/// oferecido, porque o commit local e o efeito remoto não partilham transação.
/// </remarks>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid? TrainerId { get; private set; }
    public string MessageType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public JobStatus Status { get; private set; } = null!;
    /// <summary>Chave de idempotência única, passada ao fornecedor que a suporte.</summary>
    public string IdempotencyKey { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    /// <summary>Tentativas consumidas. Alimenta o backoff e o corte para dead letter.</summary>
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public Guid? LeaseOwnerId { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private OutboxMessage() { }

    /// <summary>Cria uma mensagem pendente (sempre dentro da transação de origem).</summary>
    public OutboxMessage(
        Guid? trainerId,
        string messageType,
        string payloadJson,
        string idempotencyKey,
        Guid correlationId,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(messageType) || messageType.Length > 100)
            throw new DomainException(
                "Message type cannot be empty and must have a max length of 100 characters.");
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("Payload is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 255)
            throw new DomainException(
                "Idempotency key is required and must have a max length of 255 characters.");
        if (correlationId == Guid.Empty)
            throw new DomainException("Correlation ID is required.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        MessageType = messageType;
        Payload = payloadJson;
        Status = JobStatus.Pending;
        IdempotencyKey = idempotencyKey;
        Attempts = 0;
        CorrelationId = correlationId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>True se o dispatcher pode reclamar esta mensagem.</summary>
    public bool IsClaimable(DateTime now)
    {
        var elegibleAt = NextAttemptAt ?? CreatedAt;
        var pendingAndDue = Status == JobStatus.Pending && elegibleAt <= now;
        // Recuperação após crash: reclamada mas com lease caducado.
        var abandoned = Status == JobStatus.Processing
            && LeaseExpiresAt.HasValue
            && LeaseExpiresAt.Value <= now;
        return pendingAndDue || abandoned;
    }

    /// <summary>O dispatcher reclama a mensagem, com owner e lease.</summary>
    public void Claim(Guid leaseOwnerId, TimeSpan leaseDuration, DateTime now)
    {
        if (leaseOwnerId == Guid.Empty)
            throw new DomainException("Lease owner is required.");
        if (!IsClaimable(now))
            throw new DomainException("Message is not claimable for processing.");
        if (leaseDuration <= TimeSpan.Zero)
            throw new DomainException("Lease duration must be positive.");

        Status = JobStatus.Processing;
        LeaseOwnerId = leaseOwnerId;
        LeaseExpiresAt = now.Add(leaseDuration);
        Attempts += 1;
        UpdatedAt = now;
    }

    /// <summary>Estende o lease. Só o owner atual.</summary>
    public void RenewLease(Guid leaseOwnerId, TimeSpan leaseDuration, DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a processing message can renew its lease.");
        if (LeaseOwnerId != leaseOwnerId)
            throw new DomainException("Only the current lease owner can renew the lease.");
        if (leaseDuration <= TimeSpan.Zero)
            throw new DomainException("Lease duration must be positive.");

        if (!LeaseExpiresAt.HasValue || LeaseExpiresAt.Value <= now)
            throw new DomainException("An expired lease cannot be renewed.");

        LeaseExpiresAt = now.Add(leaseDuration);
        UpdatedAt = now;
    }

    /// <summary>O efeito foi confirmado (ex.: email aceite pelo provider).</summary>
    public void MarkCompleted(Guid leaseOwnerId, DateTime now)
    {
        if (Status != JobStatus.Processing)
            throw new DomainException("Only a claimed message can be completed.");
        if (LeaseOwnerId != leaseOwnerId)
            throw new DomainException("Only the current lease owner can complete the message.");
        if (!LeaseExpiresAt.HasValue || LeaseExpiresAt.Value <= now)
            throw new DomainException("An expired lease cannot complete the message.");

        Status = JobStatus.Completed;
        CompletedAt = now;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Regista uma falha de entrega. Só o owner do lease pode registar.</summary>
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
}
