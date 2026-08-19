namespace Application.Features.Supplements.Abstractions;

/// <summary>Persiste mutações globais e respectiva  auditoria na mesma transação.</summary>
public interface IGlobalSupplementStore
{
    Task<GlobalSupplementStoreResult> CreateAsync(
        Guid actorUserId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalSupplementStoreResult> UpdateAsync(
        Guid actorUserId,
        Guid supplementId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalSupplementStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid supplementId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalSupplementStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid supplementId,
        DateTime now,
        CancellationToken cancellationToken
    );
}
