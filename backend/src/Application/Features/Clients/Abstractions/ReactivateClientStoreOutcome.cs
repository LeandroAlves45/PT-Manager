namespace Application.Features.Clients.Abstractions;

/// <summary>Outcomes da reativação transacional de clientes.</summary>
public enum ReactivateClientStoreOutcome
{
    Reactivated,
    AlreadyActive,
    NotFound,
    UserAlreadyHasActiveRelationship,
    SubscriptionInactive,
    SubscriptionSuspended,
    SubscriptionCancelled,
    ClientLimitReached,
    SubscriptionMissing
}
