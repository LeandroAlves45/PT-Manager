namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado do commit de subscription, deduplicado e outbox.</summary>
public sealed record CommitPaymentEventStoreResult(CommitPaymentEventStoreStatus Kind);
