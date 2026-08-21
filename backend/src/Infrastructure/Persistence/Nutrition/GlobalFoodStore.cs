using System.Text.Json;
using Application.Features.Nutrition.Foods.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Persiste mutações globais de alimentos e auditoria na mesma transação.</summary>
internal sealed class GlobalFoodStore : IGlobalFoodStore
{
    private const string ResourceType = "food";
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _translator;

    public GlobalFoodStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator translator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public Task<GlobalFoodStoreResult> CreateAsync(
        Guid actorUserId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => CreateOnceAsync(
                actorUserId,
                name,
                description,
                protein,
                carbs,
                fats,
                fiber,
                now,
                cancellationToken));

    public Task<GlobalFoodStoreResult> UpdateAsync(
        Guid actorUserId,
        Guid foodId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => UpdateOnceAsync(
                actorUserId,
                foodId,
                name,
                description,
                protein,
                carbs,
                fats,
                fiber,
                now,
                cancellationToken));

    public Task<GlobalFoodStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid foodId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => SetActiveOnceAsync(
                actorUserId,
                foodId,
                isActive,
                now,
                cancellationToken));

    public Task<GlobalFoodStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken) => ExecuteAsync(
            () => DeleteOnceAsync(
                actorUserId,
                foodId,
                now,
                cancellationToken));

    private async Task<GlobalFoodStoreResult> CreateOnceAsync(
        Guid actorUserId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var food = new Food(
                null,
                name,
                description,
                protein,
                carbs,
                fats,
                fiber,
                now);
            _dbContext.Foods.Add(food);
            AddAudit(actorUserId, "create", food, null, Snapshot(food), now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // O reload fica dentro da transação: uma falha transitória antes do commit
            // pode ser repetida sem duplicar Food nem auditoria.
            await _dbContext.Entry(food).ReloadAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalFoodStoreResult.WithFood(GlobalFoodStoreResult.Status.Created, food);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<GlobalFoodStoreResult> UpdateOnceAsync(
        Guid actorUserId,
        Guid foodId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var food = await _dbContext.LockGlobalFoodAsync(foodId, cancellationToken);
            if (food is null)
                return await RollbackAsync(transaction, GlobalFoodStoreResult.Status.NotFound);
            if (!food.IsActive)
                return await RollbackAsync(transaction, GlobalFoodStoreResult.Status.Inactive);

            if (await _dbContext.MealPlanMealItems.IgnoreQueryFilters()
                .AnyAsync(item => item.FoodId == foodId, cancellationToken))
                return await RollbackAsync(transaction, GlobalFoodStoreResult.Status.Referenced);

            var before = Snapshot(food);
            food.Update(name, description, protein, carbs, fats, fiber, now);
            AddAudit(actorUserId, "update", food, before, Snapshot(food), now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalFoodStoreResult.WithFood(GlobalFoodStoreResult.Status.Updated, food);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<GlobalFoodStoreResult> SetActiveOnceAsync(
        Guid actorUserId,
        Guid foodId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var food = await _dbContext.LockGlobalFoodAsync(foodId, cancellationToken);
            if (food is null)
                return await RollbackAsync(transaction, GlobalFoodStoreResult.Status.NotFound);
            if (food.IsActive == isActive)
                return await RollbackAsync(
                    transaction, GlobalFoodStoreResult.Status.AlreadyInRequestedState);

            var before = Snapshot(food);
            food.SetActive(isActive, now);
            AddAudit(
                actorUserId,
                isActive ? "reactivate" : "archive",
                food,
                before,
                Snapshot(food),
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.Changed);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<GlobalFoodStoreResult> DeleteOnceAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var food = await _dbContext.LockGlobalFoodAsync(foodId, cancellationToken);
            if (food is null)
                return await RollbackAsync(transaction, GlobalFoodStoreResult.Status.NotFound);

            if (await _dbContext.MealPlanMealItems.IgnoreQueryFilters()
                .AnyAsync(item => item.FoodId == foodId, cancellationToken))
                return await RollbackAsync(transaction, GlobalFoodStoreResult.Status.HasReferences);

            var before = Snapshot(food);
            _dbContext.Foods.Remove(food);
            AddAudit(actorUserId, "delete", food, before, null, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.Deleted);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (_translator.TryTranslate(ex, PersistenceOperation.DeleteGlobalFood, out var error) &&
                error?.Code == "global_food_has_references")
                return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.HasReferences);
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
        Food food,
        string? before,
        string? after,
        DateTime now) =>
        _dbContext.AdministrativeAuditEntries.Add(
            new AdministrativeAuditEntry(
                actorUserId,
                action,
                ResourceType,
                food.Id,
                before,
                after,
                now));

    private static string Snapshot(Food food) => JsonSerializer.Serialize(new
    {
        id = food.Id,
        name = food.Name,
        description = food.Description,
        protein = food.Protein,
        carbs = food.Carbs,
        fats = food.Fats,
        fiber = food.Fiber,
        is_active = food.IsActive,
        created_at = food.CreatedAt,
        updated_at = food.UpdatedAt
    });

    private static async Task<GlobalFoodStoreResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        GlobalFoodStoreResult.Status status)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return GlobalFoodStoreResult.For(status);
    }

    private Task<GlobalFoodStoreResult> ExecuteAsync(
        Func<Task<GlobalFoodStoreResult>> operation)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(operation);
    }
}
