using Domain.Entities.Supplements;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Persiste mutações de suplementos privados.</summary>
public interface ISupplementStore
{
    Task AddAsync(Supplement supplement, CancellationToken cancellationToken);

    Task<SupplementStoreResult> UpdateAsync(
        Guid trainerId,
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

    Task<SupplementStoreResult> SetActiveAsync(
        Guid trainerId,
        Guid supplementId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );
}
