using System.Text.Json;
using Application.Features.Supplements.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Persiste mutações globais e auditoria na mesma transação.</summary>
internal sealed class GlobalSupplementStore : IGlobalSupplementStore
{
    private const string ResourceType = "supplement";
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _translator;

    public GlobalSupplementStore(
        PtManagerDbContext dbContext, PostgresConstraintTranslator translator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public Task<GlobalSupplementStoreResult> CreateAsync(
        Guid actorUserId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => CreateOnceAsync(
                actorUserId, name, description, unitOfMeasure,
                servingSize, timing, trainerNotes, now, cancellationToken));

    public Task<GlobalSupplementStoreResult> UpdateAsync(
        Guid actorUserId,
        Guid supplementId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => UpdateOnceAsync(
                actorUserId, supplementId, name, description, unitOfMeasure,
                servingSize, timing, trainerNotes, now, cancellationToken));

    public Task<GlobalSupplementStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid supplementId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => SetActiveOnceAsync(
                actorUserId, supplementId, isActive, now, cancellationToken));

    public Task<GlobalSupplementStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid supplementId,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => DeleteOnceAsync(
                actorUserId, supplementId, now, cancellationToken));

    private Task<GlobalSupplementStoreResult> ExecuteAsync(
        Func<Task<GlobalSupplementStoreResult>> operation)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(operation);
    }

    private async Task<GlobalSupplementStoreResult> CreateOnceAsync(
        Guid actorUserId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var supplement = new Supplement(
                null, actorUserId, name, description, unitOfMeasure,
                servingSize, timing, trainerNotes, now);
            _dbContext.Supplements.Add(supplement);
            AddAudit(actorUserId, "create", supplement, null, Snapshot(supplement), now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalSupplementStoreResult.WithSupplement(
                GlobalSupplementStoreResult.Status.Created, supplement);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<GlobalSupplementStoreResult> UpdateOnceAsync(
        Guid actorUserId,
        Guid supplementId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var supplement = await _dbContext.LockGlobalSupplementAsync(
                supplementId, cancellationToken);
            if (supplement is null)
                return await RollbackAsync(
                    transaction,
                    GlobalSupplementStoreResult.Status.NotFound);
            if (!supplement.IsActive)
                return await RollbackAsync(
                    transaction,
                    GlobalSupplementStoreResult.Status.Inactive);

            var before = Snapshot(supplement);
            supplement.Update(
                name, description, unitOfMeasure, servingSize, timing, trainerNotes, now);
            AddAudit(actorUserId, "update", supplement, before, Snapshot(supplement), now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalSupplementStoreResult.WithSupplement(
                GlobalSupplementStoreResult.Status.Updated, supplement);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<GlobalSupplementStoreResult> SetActiveOnceAsync(
        Guid actorUserId,
        Guid supplementId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var supplement = await _dbContext.LockGlobalSupplementAsync(
                supplementId, cancellationToken);
            if (supplement is null)
                return await RollbackAsync(
                    transaction,
                    GlobalSupplementStoreResult.Status.NotFound);
            if (supplement.IsActive == isActive)
                return await RollbackAsync(
                    transaction,
                    GlobalSupplementStoreResult.Status.AlreadyInRequestedState);

            var before = Snapshot(supplement);
            if (isActive)
                supplement.Reactivate(now);
            else
                supplement.Archive(now);
            AddAudit(
                actorUserId, isActive ? "reactivate" : "archive",
                supplement, before, Snapshot(supplement), now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalSupplementStoreResult.For(GlobalSupplementStoreResult.Status.Changed);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<GlobalSupplementStoreResult> DeleteOnceAsync(
        Guid actorUserId,
        Guid supplementId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var supplement = await _dbContext.LockGlobalSupplementAsync(
                supplementId, cancellationToken);
            if (supplement is null)
                return await RollbackAsync(
                    transaction,
                    GlobalSupplementStoreResult.Status.NotFound);

            var references = _dbContext.MealPlanMealSupplements
                .IgnoreQueryFilters()
                .Where(item => item.SupplementId == supplementId)
                .Select(_ => 1)
                .Concat(_dbContext.ClientSupplementAssignments
                    .IgnoreQueryFilters()
                    .Where(item => item.SupplementId == supplementId)
                    .Select(_ => 1));

            if (await references.AnyAsync(cancellationToken))
                return await RollbackAsync(
                    transaction,
                    GlobalSupplementStoreResult.Status.HasReferences);

            var before = Snapshot(supplement);
            _dbContext.Supplements.Remove(supplement);
            AddAudit(actorUserId, "delete", supplement, before, null, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalSupplementStoreResult.For(GlobalSupplementStoreResult.Status.Deleted);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (_translator.TryTranslate(
                ex,
                PersistenceOperation.DeleteGlobalSupplement,
                out var error) && error?.Code == "global_supplement_has_references")
                return GlobalSupplementStoreResult.For(
                    GlobalSupplementStoreResult.Status.HasReferences);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private void AddAudit(
        Guid actorUserId,
        string action,
        Supplement supplement,
        string? before,
        string? after,
        DateTime now) => _dbContext.AdministrativeAuditEntries.Add(
            new AdministrativeAuditEntry(
                actorUserId, action, ResourceType, supplement.Id, before, after, now));

    private static string Snapshot(Supplement supplement) => JsonSerializer.Serialize(new
    {
        id = supplement.Id,
        created_by_user_id = supplement.CreatedByUserId,
        name = supplement.Name,
        description = supplement.Description,
        unit_of_measure = supplement.UnitOfMeasure,
        serving_size = supplement.ServingSize,
        timing = supplement.Timing,
        trainer_notes = supplement.TrainerNotes,
        is_active = supplement.IsActive,
        created_at = supplement.CreatedAt,
        updated_at = supplement.UpdatedAt
    });

    private static async Task<GlobalSupplementStoreResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        GlobalSupplementStoreResult.Status status)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return GlobalSupplementStoreResult.For(status);
    }
}
