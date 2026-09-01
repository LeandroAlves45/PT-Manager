using Domain.ValueObjects;

namespace Application.Features.Administration.ContentModeration.Abstractions;

/// <summary>Persiste decisões administrativas explícitas sobre alimentos e exercícios privados.</summary>
public interface IPrivateCatalogModerationStore
{
    Task<PrivateCatalogModerationStoreResult> BlockFoodAsync(
        Guid actorUserId,
        Guid foodId,
        PlatformEnforcementReason reason,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PrivateCatalogModerationStoreResult> UnblockFoodAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PrivateCatalogModerationStoreResult> BlockExerciseAsync(
        Guid actorUserId,
        Guid exerciseId,
        PlatformEnforcementReason reason,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PrivateCatalogModerationStoreResult> UnblockExerciseAsync(
        Guid actorUserId,
        Guid exerciseId,
        DateTime now,
        CancellationToken cancellationToken);
}
