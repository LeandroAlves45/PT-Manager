using Domain.Entities.Clients;

namespace Application.Features.Clients.Abstractions;

/// <summary>
/// Porta de escrita de Clients, incluindo operações compostas com subscrição.
/// Cada método confirma a sua unidade transacional antes de devolver sucesso.
/// </summary>
public interface IClientStore
{
    /// <summary>Cria ficha e incrementa contador se existir capacidade.</summary>
    Task<CreateClientStoreOutcome> CreateWithSubscriptionSlotAsync(
        Client client,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>Carrega uma ficha filtered e tracked para alteração.</summary>
    Task<Client?> GetForUpdateAsync(
        Guid clientId,
        CancellationToken cancellationToken = default);

    /// <summary>Confirma perfil e traduz unicidades conhecidas.</summary>
    Task<SaveClientProfileOutcome> SaveProfileAsync(
        Client client,
        CancellationToken cancellationToken = default);

    /// <summary>Arquiva e decrementa contador numa única transação.</summary>
    Task<ArchiveClientStoreOutcome> ArchiveAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>Reativa e incrementa contador numa única transação.</summary>
    Task<ReactivateClientStoreOutcome> ReactivateAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default);
}
