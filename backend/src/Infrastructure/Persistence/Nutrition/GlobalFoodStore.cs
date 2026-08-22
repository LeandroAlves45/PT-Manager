using System.Data;
using System.Text.Json;
using Application.Features.Nutrition.Foods.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        CancellationToken cancellationToken)
    {
        // A mesma identidade tem de sobreviver a uma tentativa repetida após falha transitória.
        var food = new Food(null, name, description, protein, carbs, fats, fiber, now);
        var attempt = new MutationAttempt();
        return ExecuteAsync(
            token => CreateOnceAsync(actorUserId, food, now, attempt, token),
            attempt,
            cancellationToken);
    }

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
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(
            token => UpdateOnceAsync(
                actorUserId,
                foodId,
                name,
                description,
                protein,
                carbs,
                fats,
                fiber,
                now,
                attempt,
                token),
            attempt,
            cancellationToken);
    }

    public Task<GlobalFoodStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid foodId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(
            token => SetActiveOnceAsync(
                actorUserId, foodId, isActive, now, attempt, token),
            attempt,
            cancellationToken);
    }

    public async Task<GlobalFoodStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        try
        {
            return await ExecuteAsync(
                token => DeleteOnceAsync(actorUserId, foodId, now, attempt, token),
                attempt,
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (_translator.TryTranslate(
                ex,
                PersistenceOperation.DeleteGlobalFood,
                out var error) && error?.Code == "global_food_has_references")
                return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.HasReferences);
            throw;
        }
    }

    private async Task<GlobalFoodStoreResult> CreateOnceAsync(
        Guid actorUserId,
        Food food,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        _dbContext.Foods.Add(food);
        attempt.AuditEntry = AddAudit(
            actorUserId, "create", food, null, Snapshot(food), now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalFoodStoreResult.WithFood(GlobalFoodStoreResult.Status.Created, food);
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
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var food = await _dbContext.LockGlobalFoodAsync(foodId, cancellationToken);
        if (food is null)
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.NotFound);
        if (!food.IsActive)
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.Inactive);

        if (await _dbContext.MealPlanMealItems.IgnoreQueryFilters()
            .AnyAsync(item => item.FoodId == foodId, cancellationToken))
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.Referenced);

        var before = Snapshot(food);
        food.Update(name, description, protein, carbs, fats, fiber, now);
        attempt.AuditEntry = AddAudit(
            actorUserId, "update", food, before, Snapshot(food), now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalFoodStoreResult.WithFood(GlobalFoodStoreResult.Status.Updated, food);
    }

    private async Task<GlobalFoodStoreResult> SetActiveOnceAsync(
        Guid actorUserId,
        Guid foodId,
        bool isActive,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var food = await _dbContext.LockGlobalFoodAsync(foodId, cancellationToken);
        if (food is null)
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.NotFound);
        if (food.IsActive == isActive)
            return GlobalFoodStoreResult.For(
                GlobalFoodStoreResult.Status.AlreadyInRequestedState);

        var before = Snapshot(food);
        food.SetActive(isActive, now);
        attempt.AuditEntry = AddAudit(
            actorUserId,
            isActive ? "reactivate" : "archive",
            food,
            before,
            Snapshot(food),
            now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.Changed);
    }

    private async Task<GlobalFoodStoreResult> DeleteOnceAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var food = await _dbContext.LockGlobalFoodAsync(foodId, cancellationToken);
        if (food is null)
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.NotFound);

        if (await _dbContext.MealPlanMealItems.IgnoreQueryFilters()
            .AnyAsync(item => item.FoodId == foodId, cancellationToken))
            return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.HasReferences);

        var before = Snapshot(food);
        _dbContext.Foods.Remove(food);
        attempt.AuditEntry = AddAudit(actorUserId, "delete", food, before, null, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.Deleted);
    }

    private AdministrativeAuditEntry AddAudit(
        Guid actorUserId,
        string action,
        Food food,
        string? before,
        string? after,
        DateTime now)
    {
        var entry = new AdministrativeAuditEntry(
            actorUserId, action, ResourceType, food.Id, before, after, now);
        _dbContext.AdministrativeAuditEntries.Add(entry);
        return entry;
    }

    private static string Snapshot(Food food) => JsonSerializer.Serialize(new
    {
        id = food.Id,
        name = food.Name,
        description = food.Description,
        protein = food.Protein,
        carbs = food.Carbs,
        fats = food.Fats,
        // A auditoria é criada antes do INSERT; a fórmula reproduz a coluna generated
        // para o snapshot guardar o valor correto sem separar a escrita atómica.
        kcal = food.Protein * 4 + food.Carbs * 4 + food.Fats * 9,
        fiber = food.Fiber,
        is_active = food.IsActive,
        created_at = food.CreatedAt,
        updated_at = food.UpdatedAt
    });

    private Task<GlobalFoodStoreResult> ExecuteAsync(
        Func<CancellationToken, Task<GlobalFoodStoreResult>> operation,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            async operationToken =>
            {
                // Uma tentativa repetida reconstrói tracking e a prova do commit.
                _dbContext.ChangeTracker.Clear();
                attempt.AuditEntry = null;
                return await operation(operationToken);
            },
            verificationToken => VerifySucceededAsync(attempt, verificationToken),
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    private Task<bool> VerifySucceededAsync(
        MutationAttempt attempt,
        CancellationToken cancellationToken) =>
        attempt.AuditEntry is null
            ? Task.FromResult(false)
            : _dbContext.AdministrativeAuditEntries
                .AsNoTracking()
                .AnyAsync(entry => entry.Id == attempt.AuditEntry.Id, cancellationToken);

    private sealed class MutationAttempt
    {
        public AdministrativeAuditEntry? AuditEntry { get; set; }
    }
}
