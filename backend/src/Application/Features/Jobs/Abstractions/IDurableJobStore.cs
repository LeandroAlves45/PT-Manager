using Domain.Entities.Jobs;

namespace Application.Features.Jobs.Abstractions;

/// <summary>
/// Acesso á fila de jobs duráveis. Os métodos de claim, renovação e conclusão
/// executam UPDATE condicional atómico no PostgreSQL -> as garantias de
/// concorrência vêm da base de dados, não dos guards do Domain.
/// </summary>
public interface IDurableJobStore
{
    /// <summary>
    /// Reclama até <paramref name="batchSize"/> jobs elegíveis. O store cria um
    /// token opaco novo para esta execução de claim.
    /// </summary>
    Task<IReadOnlyList<DurableJob>> ClaimDueJobsAsync(
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Estende o lease. Devolve false se o lease já não pertencer a este worker
    /// -> nesse caso, o worker deve abortar e descartar o job em curso.
    /// </summary>
    Task<bool> TryRenewLeaseAsync(
        Guid jobId,
        Guid leaseOwnerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    /// <summary>Conclui o job. False se o lease foi perdido.</summary>
    Task<bool> TryCompleteAsync(Guid jobId, Guid leaseOwnerId, CancellationToken cancellationToken);

    /// <summary>
    /// Regista falha e agenda retry ou move para dead letter se as tentativas estiverem esgotadas.
    /// False se o lease foi perdido.
    /// </summary>
    Task<bool> TryRecordFailureAsync(
        Guid jobId,
        Guid leaseOwnerId,
        string sanitizedError,
        DateTime? nextAttemptAt,
        CancellationToken cancellationToken);
}
