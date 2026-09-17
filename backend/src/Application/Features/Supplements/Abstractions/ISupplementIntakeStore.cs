namespace Application.Features.Supplements.Abstractions;

/// <summary>Marca e desmarca a toma de hoje de uma atribuição do cliente autenticado.</summary>
public interface ISupplementIntakeStore
{
    Task<SupplementIntakeStoreResult> MarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid assignmentId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<SupplementIntakeStoreResult> UnmarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid assignmentId,
        DateTime now,
        CancellationToken cancellationToken);
}
