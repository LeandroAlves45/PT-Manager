using Domain.Entities.Billing;

namespace Application.Features.Packs.PackTypes.Abstractions;

/// <summary>Persiste mutações tenant-safe de tipos de packs privados.</summary>
public interface IPackTypeStore
{
    Task AddAsync(PackType packType, CancellationToken cancellationToken);

    Task<PackTypeStoreResult> UpdateAsync(
        Guid packTypeId,
        Guid trainerId,
        string name,
        int sessionCount,
        int priceCents,
        string currency,
        int? expectedDurationDays,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<PackTypeStoreResult> SetActiveAsync(
        Guid packTypeId,
        Guid trainerId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );
}
