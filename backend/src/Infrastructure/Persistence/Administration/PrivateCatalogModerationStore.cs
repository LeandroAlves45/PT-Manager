using System.Data;
using System.Text.Json;
using Application.Features.Administration.ContentModeration.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Administration;

/// <summary>Aplica enforcement por ID e grava a auditoria na mesma transação.</summary>
internal sealed class PrivateCatalogModerationStore : IPrivateCatalogModerationStore
{
    private readonly PtManagerDbContext _dbContext;

    public PrivateCatalogModerationStore(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<PrivateCatalogModerationStoreResult> BlockFoodAsync(
        Guid actorUserId,
        Guid foodId,
        PlatformEnforcementReason reason,
        string? note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var attempt = new MutationAttempt();
        return ExecuteAsync(token => ChangeFoodOnceAsync(
            actorUserId,
            foodId,
            reason,
            note,
            shouldBlock: true,
            now,
            attempt,
            token),
        attempt,
        cancellationToken);
    }

    public Task<PrivateCatalogModerationStoreResult> UnblockFoodAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(token => ChangeFoodOnceAsync(
            actorUserId,
            foodId,
            reason: null,
            note: null,
            shouldBlock: false,
            now,
            attempt,
            token),
        attempt,
        cancellationToken);
    }

    public Task<PrivateCatalogModerationStoreResult> BlockExerciseAsync(
        Guid actorUserId,
        Guid exerciseId,
        PlatformEnforcementReason reason,
        string? note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var attempt = new MutationAttempt();
        return ExecuteAsync(token => ChangeExerciseOnceAsync(
            actorUserId,
            exerciseId,
            reason,
            note,
            shouldBlock: true,
            now,
            attempt,
            token),
        attempt,
        cancellationToken);
    }

    public Task<PrivateCatalogModerationStoreResult> UnblockExerciseAsync(
        Guid actorUserId,
        Guid exerciseId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(token => ChangeExerciseOnceAsync(
            actorUserId,
            exerciseId,
            reason: null,
            note: null,
            shouldBlock: false,
            now,
            attempt,
            token),
        attempt,
        cancellationToken);
    }

    private async Task<PrivateCatalogModerationStoreResult> ChangeFoodOnceAsync(
        Guid actorUserId,
        Guid foodId,
        PlatformEnforcementReason? reason,
        string? note,
        bool shouldBlock,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveSuperuserAsync(actorUserId, cancellationToken))
            return PrivateCatalogModerationStoreResult.ActorInvalid;

        var food = await _dbContext.Foods
            .FromSqlInterpolated($"""
                SELECT * FROM foods WHERE id = {foodId} FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

        if (food is null)
            return PrivateCatalogModerationStoreResult.NotFound;
        if (!food.OwnerTrainerId.HasValue)
            return PrivateCatalogModerationStoreResult.NotPrivate;

        var before = Snapshot(food);
        var changed = shouldBlock ? food.Block(reason!, now, note) : food.Unblock(now);
        if (!changed)
            return PrivateCatalogModerationStoreResult.AlreadyInRequestedState;
        if (!shouldBlock)
            await RemoveFoodFromPlansAsync(food.Id, cancellationToken);

        attempt.AuditEntry = AddAudit(
            actorUserId,
            shouldBlock ? "food_platform_blocked" : "food_platform_unblocked",
            "food",
            food.Id,
            before,
            Snapshot(food),
            now
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PrivateCatalogModerationStoreResult.Changed;
    }

    private async Task<PrivateCatalogModerationStoreResult> ChangeExerciseOnceAsync(
        Guid actorUserId,
        Guid exerciseId,
        PlatformEnforcementReason? reason,
        string? note,
        bool shouldBlock,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveSuperuserAsync(actorUserId, cancellationToken))
            return PrivateCatalogModerationStoreResult.ActorInvalid;

        var exercise = await _dbContext.Exercises
            .FromSqlInterpolated($"""
                SELECT * FROM exercises WHERE id = {exerciseId} FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (exercise is null)
            return PrivateCatalogModerationStoreResult.NotFound;
        if (!exercise.OwnerTrainerId.HasValue)
            return PrivateCatalogModerationStoreResult.NotPrivate;

        var before = Snapshot(exercise);
        var changed = shouldBlock ? exercise.Block(reason!, now, note) : exercise.Unblock(now);
        if (!changed)
            return PrivateCatalogModerationStoreResult.AlreadyInRequestedState;
        if (!shouldBlock)
            await RemoveExerciseFromPlansAsync(exercise.Id, now, cancellationToken);

        attempt.AuditEntry = AddAudit(
            actorUserId,
            shouldBlock ? "exercise_platform_blocked" : "exercise_platform_unblocked",
            "exercise",
            exercise.Id,
            before,
            Snapshot(exercise),
            now
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PrivateCatalogModerationStoreResult.Changed;
    }

    public Task<PrivateCatalogModerationStoreResult> BlockSupplementAsync(
        Guid actorUserId,
        Guid supplementId,
        PlatformEnforcementReason reason,
        string? note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        var attempt = new MutationAttempt();
        return ExecuteAsync(token => ChangeSupplementOnceAsync(
            actorUserId, supplementId, reason, note, shouldBlock: true, now, attempt, token),
        attempt,
        cancellationToken);
    }

    public Task<PrivateCatalogModerationStoreResult> UnblockSupplementAsync(
        Guid actorUserId,
        Guid supplementId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(token => ChangeSupplementOnceAsync(
            actorUserId, supplementId, reason: null, note: null, shouldBlock: false, now, attempt, token),
        attempt,
        cancellationToken);
    }

    private async Task<PrivateCatalogModerationStoreResult> ChangeSupplementOnceAsync(
        Guid actorUserId,
        Guid supplementId,
        PlatformEnforcementReason? reason,
        string? note,
        bool shouldBlock,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveSuperuserAsync(actorUserId, cancellationToken))
            return PrivateCatalogModerationStoreResult.ActorInvalid;

        var supplement = await _dbContext.Supplements
            .FromSqlInterpolated($"""
                SELECT * FROM supplements WHERE id = {supplementId} FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (supplement is null)
            return PrivateCatalogModerationStoreResult.NotFound;
        if (!supplement.OwnerTrainerId.HasValue)
            return PrivateCatalogModerationStoreResult.NotPrivate;

        var before = Snapshot(supplement);
        var changed = shouldBlock ? supplement.Block(reason!, now, note) : supplement.Unblock(now);
        if (!changed)
            return PrivateCatalogModerationStoreResult.AlreadyInRequestedState;
        if (!shouldBlock)
            await RemoveSupplementFromPlansAsync(supplement.Id, now, cancellationToken);

        attempt.AuditEntry = AddAudit(
            actorUserId,
            shouldBlock ? "supplement_platform_blocked" : "supplement_platform_unblocked",
            "supplement",
            supplement.Id,
            before,
            Snapshot(supplement),
            now
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PrivateCatalogModerationStoreResult.Changed;
    }

    // Levantar o bloqueio torna o item outra vez utilizável na biblioteca e tira-o dos planos
    // onde estava: o cliente não o vê até o personal trainer o voltar a pôr. O contexto
    // administrativo não tem tenant: sem IgnoreQueryFilters os filtros escondem todas as linhas
    // e o ExecuteDelete não apaga nada, sem erro.
    private Task RemoveFoodFromPlansAsync(Guid foodId, CancellationToken cancellationToken) =>
        _dbContext.MealPlanMealItems
            .IgnoreQueryFilters()
            .Where(item => item.FoodId == foodId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <summary>
    /// Tira o exercício dos planos. Onde o cliente já registou séries, a linha fica no plano
    /// marcada como retirada, para o histórico não se perder; nas outras sai (com as séries
    /// planeadas).
    /// </summary>
    private async Task RemoveExerciseFromPlansAsync(Guid exerciseId, DateTime now, CancellationToken cancellationToken)
    {
        var withHistory = _dbContext.TrainingPlanDayExercises
            .IgnoreQueryFilters()
            .Where(item => item.ExerciseId == exerciseId &&
                _dbContext.ClientExerciseSetLogs.IgnoreQueryFilters()
                    .Any(log => log.TrainingPlanDayExerciseId == item.Id));

        await withHistory
            .Where(item => item.RetiredAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.RetiredAt, now), cancellationToken);

        await _dbContext.TrainingPlanDayExercises
            .IgnoreQueryFilters()
            .Where(item => item.ExerciseId == exerciseId &&
                !_dbContext.ClientExerciseSetLogs.IgnoreQueryFilters()
                    .Any(log => log.TrainingPlanDayExerciseId == item.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Tira o suplemento das refeições e do plano fora das refeições (as tomas ficam, sem a
    /// prescrição) e desativa as atribuições diretas.
    /// </summary>
    private async Task RemoveSupplementFromPlansAsync(Guid supplementId, DateTime now, CancellationToken cancellationToken)
    {
        await _dbContext.MealPlanMealSupplements
            .IgnoreQueryFilters()
            .Where(item => item.SupplementId == supplementId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.MealPlanSupplements
            .IgnoreQueryFilters()
            .Where(item => item.SupplementId == supplementId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ClientSupplementAssignments
            .IgnoreQueryFilters()
            .Where(item => item.SupplementId == supplementId && item.IsActive)
            .ExecuteUpdateAsync(set => set
                .SetProperty(item => item.IsActive, false)
                .SetProperty(item => item.UpdatedAt, now),
            cancellationToken);
    }

    private Task<bool> IsActiveSuperuserAsync(Guid actorUserId, CancellationToken cancellationToken) =>
        _dbContext.Users
            .FromSqlInterpolated($"""
                SELECT * FROM users WHERE id = {actorUserId} FOR SHARE
            """)
            .IgnoreQueryFilters()
            .AnyAsync(user =>
                user.Role == "superuser" && user.IsActive && !user.IsDeleted, cancellationToken);

    private AdministrativeAuditEntry AddAudit(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        string before,
        string after,
        DateTime now
    )
    {
        var entry = new AdministrativeAuditEntry(
            actorUserId,
            action,
            resourceType,
            resourceId,
            before,
            after,
            now
        );
        _dbContext.AdministrativeAuditEntries.Add(entry);
        return entry;
    }

    private static string Snapshot(Food food) => JsonSerializer.Serialize(new
    {
        id = food.Id,
        owner_trainer_id = food.OwnerTrainerId,
        platform_enforcement_status = food.PlatformEnforcementStatus.Value,
        platform_enforcement_reason = food.PlatformEnforcementReason?.Value,
        platform_enforcement_note = food.PlatformEnforcementNote,
        platform_enforced_at = food.PlatformEnforcedAt
    });

    private static string Snapshot(Exercise exercise) => JsonSerializer.Serialize(new
    {
        id = exercise.Id,
        owner_trainer_id = exercise.OwnerTrainerId,
        platform_enforcement_status = exercise.PlatformEnforcementStatus.Value,
        platform_enforcement_reason = exercise.PlatformEnforcementReason?.Value,
        platform_enforcement_note = exercise.PlatformEnforcementNote,
        platform_enforced_at = exercise.PlatformEnforcedAt
    });

    private static string Snapshot(Domain.Entities.Supplements.Supplement supplement) => JsonSerializer.Serialize(new
    {
        id = supplement.Id,
        owner_trainer_id = supplement.OwnerTrainerId,
        platform_enforcement_status = supplement.PlatformEnforcementStatus.Value,
        platform_enforcement_reason = supplement.PlatformEnforcementReason?.Value,
        platform_enforcement_note = supplement.PlatformEnforcementNote,
        platform_enforced_at = supplement.PlatformEnforcedAt
    });

    private Task<PrivateCatalogModerationStoreResult> ExecuteAsync(
        Func<CancellationToken, Task<PrivateCatalogModerationStoreResult>> operation,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            async operationToken =>
            {
                _dbContext.ChangeTracker.Clear();
                attempt.AuditEntry = null;
                return await operation(operationToken);
            },
            verificationToken => attempt.AuditEntry is null
                ? Task.FromResult(false)
                : _dbContext.AdministrativeAuditEntries
                    .AsNoTracking()
                    .AnyAsync(entry => entry.Id == attempt.AuditEntry!.Id, verificationToken),
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    private sealed class MutationAttempt
    {
        public AdministrativeAuditEntry? AuditEntry { get; set; }
    }
}
