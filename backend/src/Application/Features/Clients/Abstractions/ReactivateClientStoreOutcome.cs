namespace Application.Features.Clients.Abstractions;

/// <summary>Outcomes da reativação transacional de clientes.</summary>
public enum ReactivateClientStoreOutcome
{
    /// <summary>Transição e contador confirmados.</summary>
    Reactivated,
    /// <summary>Já estava ativo; contador não mudou.</summary>
    AlreadyActive,
    /// <summary>Inexistente ou invisível no tenant.</summary>
    NotFound,
    /// <summary>Subscrição inativa.</summary>
    SubscriptionInactive,
    /// <summary>Subscrição suspensa.</summary>
    SubscriptionSuspended,
    /// <summary>Subscrição cancelada.</summary>
    SubscriptionCancelled,
    /// <summary>Limite de clientes atingido.</summary>
    ClientLimitReached,
    /// <summary>Subscrição obrigatória não existe.</summary>
    SubscriptionMissing
}
