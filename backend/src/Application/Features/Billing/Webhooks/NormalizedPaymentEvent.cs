namespace Application.Features.Billing.Webhooks;

/// <summary>Evento já autenticado e normalizado pela futura fronteira HTTP.</summary>
public sealed record NormalizedPaymentEvent(
    string EventId,
    string EventType,
    PaymentEventKind Kind,
    string? ProviderCustomerId,
    string? ProviderSubscriptionId,
    string? ProviderStatus,
    Guid CorrelationId,
    DateTime CreatedAt
);
