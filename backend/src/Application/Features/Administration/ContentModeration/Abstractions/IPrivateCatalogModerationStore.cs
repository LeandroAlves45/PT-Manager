using Domain.ValueObjects;

namespace Application.Features.Administration.ContentModeration.Abstractions;

/// <summary>
/// Persiste decisões administrativas explícitas sobre alimentos, exercícios e suplementos
/// privados. Levantar um bloqueio tira o item dos planos onde estava.
/// </summary>
public interface IPrivateCatalogModerationStore
{
    Task<PrivateCatalogModerationStoreResult> BlockFoodAsync(
        Guid actorUserId,
        Guid foodId,
        PlatformEnforcementReason reason,
        string? note,
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
        string? note,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PrivateCatalogModerationStoreResult> UnblockExerciseAsync(
        Guid actorUserId,
        Guid exerciseId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PrivateCatalogModerationStoreResult> BlockSupplementAsync(
        Guid actorUserId,
        Guid supplementId,
        PlatformEnforcementReason reason,
        string? note,
        DateTime now,
        CancellationToken cancellationToken);

    Task<PrivateCatalogModerationStoreResult> UnblockSupplementAsync(
        Guid actorUserId,
        Guid supplementId,
        DateTime now,
        CancellationToken cancellationToken);
}
