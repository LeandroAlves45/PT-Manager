namespace Application.Features.Jobs.Dispatching;

/// <summary>Snapshot imutável de um durable job reclamado.</summary>
public sealed record DurableJobEnvelope(
    Guid Id,
    Guid? TrainerId,
    string JobType,
    int JobVersion,
    string Payload,
    string IdempotencyKey,
    Guid CorrelationId,
    int Attempts,
    Guid LeaseOwnerId
);
