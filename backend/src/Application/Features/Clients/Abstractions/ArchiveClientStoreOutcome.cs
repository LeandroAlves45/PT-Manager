namespace Application.Features.Clients.Abstractions;

/// <summary>Outcomes do arquivo transacional.</summary>
public enum ArchiveClientStoreOutcome
{
    /// <summary>Transição e contador confirmados.</summary>
    Archived,
    /// <summary>Já estava arquivado; contador não mudou.</summary>
    AlreadyArchived,
    /// <summary>Inexistente ou invisível no tenant.</summary>
    NotFound,
    /// <summary>Subscrição obrigatória não existe.</summary>
    SubscriptionMissing
}
