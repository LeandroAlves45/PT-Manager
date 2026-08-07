namespace Application.Features.Clients.Abstractions;

/// <summary>Outcomes atómicos da criação de clientes.</summary>
public enum CreateClientStoreOutcome
{
    /// <summary>Cliente e contador foram confirmados.</summary>
    Created,
    /// <summary>Email colide com outro cliente do tenant.</summary>
    DuplicateEmail,
    /// <summary>Telefone colide com outro cliente do tenant.</summary>
    DuplicatePhone,
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
