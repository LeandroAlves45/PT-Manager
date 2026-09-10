namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado provider-neutral da letura autoritativa de uma subscrição.</summary>
public sealed record SubscriptionReconciliationOutcome(
    SubscriptionReconciliationStatus Status,
    ProviderSubscriptionSnapshot? Snapshot = null,
    string? FailureCode = null);
