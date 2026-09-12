namespace Application.Features.Clients.Abstractions;

/// <summary>Outcomes atómicos da criação de clientes.</summary>
public enum CreateClientStoreOutcome
{

    Created,
    DuplicateEmail,
    DuplicatePhone,
    SubscriptionInactive,
    SubscriptionSuspended,
    SubscriptionCancelled,
    ClientLimitReached,
    SubscriptionMissing
}
