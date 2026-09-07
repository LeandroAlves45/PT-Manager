namespace Application.Features.Jobs.Dispatching;

/// <summary>Snapshot imutável de uma mensagem de outbox reclamada.</summary>
public sealed record OutboxMessageEnvelope(
    Guid Id,
    Guid? TrainerId,
    string MessageType,
    string Payload,
    string IdempotencyKey,
    Guid CorrelationId,
    int Attempts,
    Guid LeaseOwnerId
);
