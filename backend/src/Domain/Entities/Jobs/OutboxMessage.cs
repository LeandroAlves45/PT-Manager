using Domain.Exceptions;

namespace Domain.Entities.Jobs;

/// <summary>
/// Mensagem de outbox transacional: escrita na mesma transação da alteração
/// de dominio que a originou (ex: webhook Stripe processado -> email de confirmação por enviar).
/// O dispatcher entrega de forma idempotente.
/// </summary>
/// <remarks>
/// Estados: pending → dispatched → completed | failed.
/// 'dispatched' significa "entregue ao mecanismo de execução"; só passa a
/// 'completed' quando o efeito (ex.: email enviado) é confirmado.
/// </remarks>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid? TrainerId { get; private set; }
    public string MessageType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private OutboxMessage() { }

    /// <summary>Cria uma mensagem pendente (sempre dentro da transação de origem).</summary>
    public OutboxMessage(
        Guid? trainerId,
        string messageType,
        string payloadJson,
        Guid correlationId,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(messageType))
            throw new DomainException("Message type cannot be empty.");
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("Payload is required.");
        if (correlationId == Guid.Empty)
            throw new DomainException("Correlation ID is required.");
        if (messageType.Length > 100)
            throw new DomainException("Message type cannot exceed 100 characters.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        MessageType = messageType;
        Payload = payloadJson;
        Status = "pending";
        CorrelationId = correlationId;
        CreatedAt = now;
    }

    /// <summary>O dispatcher assume a mensagem.</summary>
    public void MarkDispatched(DateTime now)
    {
        if (Status != "pending")
            throw new DomainException("Only one pending message can be marked as dispatched.");

        Status = "dispatched";
        DispatchedAt = now;
    }

    /// <summary>O efeito foi confirmado (ex.: email aceite pelo provider).</summary>
    public void MarkCompleted(DateTime now)
    {
        if (Status != "dispatched")
            throw new DomainException("Only one dispatched message can be marked as completed.");

        Status = "completed";
        CompletedAt = now;
    }

    /// <summary>O efeito falhou definitivamente, volta a "failed" para triagem.</summary>
    public void MarkFailed(DateTime now)
    {
        if (Status != "dispatched")
            throw new DomainException("Only one dispatched message can be marked as failed.");

        Status = "failed";
    }
}
