using Domain.Exceptions;
namespace Domain.Entities.Billing;

/// <summary>
/// Registo de um evento Stripe já processado. A unicidade de StripeEventId
/// garante que reentregas do mesmo evento (retries do Stripe) não produzem efeitos duplicados.
/// </summary>
public class ProcessedStripeEvent
{
    public Guid Id { get; private set; }
    public string StripeEventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public DateTime ProcessedAt { get; private set; }

    private ProcessedStripeEvent() { }

    /// <summary>Regista um evento como processado.</summary>
    public ProcessedStripeEvent(
        string stripeEventId,
        string eventType,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(stripeEventId))
            throw new DomainException("Stripe event ID cannot be empty.");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("Stripe event type cannot be empty.");
        if (stripeEventId.Length > 255)
            throw new DomainException("Stripe event ID cannot exceed 255 characters.");
        if (eventType.Length > 100)
            throw new DomainException("Stripe event type cannot exceed 100 characters.");

        Id = Guid.NewGuid();
        StripeEventId = stripeEventId;
        EventType = eventType;
        ProcessedAt = now;
    }
}
