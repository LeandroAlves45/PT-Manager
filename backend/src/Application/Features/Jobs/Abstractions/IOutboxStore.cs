using Domain.Entities.Jobs;

namespace Application.Features.Jobs.Abstractions;

/// <summary>
/// Acesso á outbox transacional. Entrega at-least-once: uma mensagem pode ser entregue
/// mais do que uma vez se o processo terminar depois de o fornecedor externo aceitar
/// o efeito e antes do commit local.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Reclama mensagens elegíveis e cria um token opaco novo para este claim.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renova o lease apenas se a mensagem continuar em processamento,
    /// pertencer ao worker e ainda não tiver expirado.
    /// </summary>
    Task<bool> TryRenewLeaseAsync(
        Guid messageId,
        Guid leaseOwnerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca como entregue. False se o lease foi perdido.
    /// </summary>
    Task<bool> TryCompleteAsync(Guid messageId, Guid leaseOwnerId, CancellationToken cancellationToken);

    /// <summary>
    /// Regista falha e agenda retry ou move para dead letter. False se o lease foi perdido.
    /// </summary>
    Task<bool> TryRecordFailureAsync(
        Guid messageId,
        Guid leaseOwnerId,
        string sanitizedError,
        DateTime? nextAttemptAt,
        CancellationToken cancellationToken);
}
