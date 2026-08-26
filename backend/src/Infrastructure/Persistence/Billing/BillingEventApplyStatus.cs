namespace Infrastructure.Persistence.Billing;

/// <summary>Classifica o efeito local de um evento autenticado.</summary>
internal enum BillingEventApplyStatus
{
    Applied,
    StaleSnapshot,
    NoStateChange,
    ReconciliationRequired,
    ExternalIdentityConflict
}
