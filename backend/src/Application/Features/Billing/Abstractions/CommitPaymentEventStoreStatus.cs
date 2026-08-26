namespace Application.Features.Billing.Abstractions;

/// <summary>Resultados esperados do commit atómico.</summary>
public enum CommitPaymentEventStoreStatus
{
    Processed,
    AlreadyProcessed,
    SubscriptionNotFound,
    ExternalIdentityConflict,
    ReconciliationRequired,
    ConcurrencyConflict
}
