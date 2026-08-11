using Application.Features.Nutrition.MealPlans;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Persiste planos alimentares como operações compostas e tenant-safe.</summary>
internal sealed class MealPlanStore : IMealPlanStore
{
    private readonly PtManagerDbContext _dbContext;

    public MealPlanStore(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<MealPlanStoreResult> CreateAsync(
        Guid trainerId,
        CreateMealPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        ValidateArguments(trainerId, model);
        cancellationToken.ThrowIfCancellationRequested();
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Uma tentativa repetida não pode reutilizar entidades tracked pela tentativa anterior.
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                var clientExists = await _dbContext.Clients.AsNoTracking()
                    .AnyAsync(
                        client => client.Id == model.ClientId && client.IsActive,
                        cancellationToken
                    );
                if (!clientExists)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MealPlanStoreResult.ForClientNotFound();
                }

                var referenceStatus = await ValidateCatalogReferencesAsync(
                    model.Structure.Meals.SelectMany(meal => meal.Items)
                        .Select(item => item.FoodId).Distinct().ToArray(),
                    model.Structure.Meals.SelectMany(meal => meal.Supplements)
                        .Select(item => item.SupplementId).Distinct().ToArray(),
                    cancellationToken
                );

                var referenceFailure = MapCatalogFailure(referenceStatus);
                if (referenceFailure is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return referenceFailure;
                }

                var plan = new MealPlan(
                    trainerId,
                    model.ClientId,
                    model.Name,
                    model.Description,
                    model.StartsDate,
                    model.EndsDate,
                    model.Calculation,
                    now
                );

                AddNewStructure(plan, model.Structure, now);
                _dbContext.MealPlans.Add(plan);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MealPlanStoreResult.ForCreated(plan.Id);
            }
            catch
            {
                // Cleanup de melhor esforço: nunca usar o token do request, que pode já
                // estar cancelado (é frequentemente a própria causa da falha aqui apanhada).
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    public async Task<MealPlanStoreResult> UpdateAsync(
        Guid trainerId,
        UpdateMealPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        ValidateArguments(trainerId, model);
        cancellationToken.ThrowIfCancellationRequested();
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // O lock e a árvore são reconstruídos integralmente em cada tentativa.
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                var lockedPlanId = await _dbContext.Database.SqlQuery<Guid>(
                    $"SELECT id AS \"Value\" FROM meal_plans WHERE id = {model.MealPlanId} AND owner_trainer_id = {trainerId} AND is_deleted = false FOR UPDATE"
                ).SingleOrDefaultAsync(cancellationToken);
                if (lockedPlanId == Guid.Empty)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MealPlanStoreResult.ForNotFound();
                }

                var plan = await _dbContext.MealPlans
                    .Where(candidate => candidate.Id == lockedPlanId
                        && candidate.OwnerTrainerId == trainerId)
                    .Include(candidate => candidate.Meals)
                        .ThenInclude(meal => meal.Items)
                    .Include(candidate => candidate.Meals)
                        .ThenInclude(meal => meal.Supplements)
                    .AsSplitQuery()
                    .SingleAsync(cancellationToken);

                if (!ReferenceBelongToAggregate(plan, model.Structure))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MealPlanStoreResult.ForStructureReferenceNotFound();
                }

                var referenceStatus = await ValidateChangedReferencesAsync(
                    plan,
                    model.Structure,
                    cancellationToken
                );
                var referenceFailure = MapCatalogFailure(referenceStatus);
                if (referenceFailure is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return referenceFailure;
                }

                Reconcile(plan, model, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MealPlanStoreResult.ForUpdated(plan.Id);
            }
            catch
            {
                // Cleanup de melhor esforço: nunca usar o token do request, que pode já
                // estar cancelado (é frequentemente a própria causa da falha aqui apanhada).
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    public async Task<MealPlanStoreResult> SetArchivedAsync(
        Guid mealPlanId,
        Guid trainerId,
        bool isArchived,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var affected = await _dbContext.MealPlans
            .Where(plan => plan.Id == mealPlanId && plan.OwnerTrainerId == trainerId)
            .Where(plan => plan.IsArchived != isArchived)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(plan => plan.IsArchived, isArchived)
                    .SetProperty(plan => plan.IsActive, !isArchived)
                    .SetProperty(plan => plan.UpdatedAt, now),
                cancellationToken
            );
        if (affected == 1)
            return MealPlanStoreResult.ForChanged();
        if (affected > 1)
            throw new InvalidOperationException(
                "A MealPlan ID must identify at most one row."
            );

        var state = await _dbContext.MealPlans.AsNoTracking()
            .Where(plan => plan.Id == mealPlanId && plan.OwnerTrainerId == trainerId)
            .Select(plan => (bool?)plan.IsArchived)
            .SingleOrDefaultAsync(cancellationToken);
        if (!state.HasValue)
            return MealPlanStoreResult.ForNotFound();
        if (state.Value == isArchived)
            return MealPlanStoreResult.ForAlreadyRequested();
        throw new InvalidOperationException("MealPlan state changed unexpectedly.");
    }

    private async Task<CatalogReferenceStatus> ValidateChangedReferencesAsync(
        MealPlan plan,
        MealPlanStructureInput structure,
        CancellationToken cancellationToken
    )
    {
        var items = plan.Meals.SelectMany(meal => meal.Items).ToDictionary(item => item.Id);
        var associations = plan.Meals.SelectMany(meal => meal.Supplements)
            .ToDictionary(item => item.Id);

        var changedFoodIds = structure.Meals.SelectMany(meal => meal.Items)
            .Where(input => !input.Id.HasValue
                || items[input.Id.Value].FoodId != input.FoodId)
            .Select(input => input.FoodId)
            .Distinct()
            .ToArray();
        var changedSupplementIds = structure.Meals.SelectMany(meal => meal.Supplements)
            .Where(input => !input.Id.HasValue
                || associations[input.Id.Value].SupplementId != input.SupplementId)
            .Select(input => input.SupplementId)
            .Distinct()
            .ToArray();

        return await ValidateCatalogReferencesAsync(
            changedFoodIds,
            changedSupplementIds,
            cancellationToken
        );
    }

    private async Task<CatalogReferenceStatus> ValidateCatalogReferencesAsync(
        IReadOnlyCollection<Guid> foodIds,
        IReadOnlyCollection<Guid> supplementIds,
        CancellationToken cancellationToken
    )
    {
        var foods = await _dbContext.Foods.AsNoTracking()
            .Where(food => foodIds.Contains(food.Id))
            .Select(food => new { food.Id, food.IsActive })
            .ToListAsync(cancellationToken);
        var supplements = await _dbContext.Supplements.AsNoTracking()
            .Where(item => supplementIds.Contains(item.Id))
            .Select(item => new { item.Id, item.IsActive })
            .ToListAsync(cancellationToken);

        if (foods.Select(food => food.Id).ToHashSet().SetEquals(foodIds)
            && supplements.Select(item => item.Id).ToHashSet().SetEquals(supplementIds))
        {
            return foods.Any(food => !food.IsActive)
                || supplements.Any(item => !item.IsActive)
                ? CatalogReferenceStatus.Inactive
                : CatalogReferenceStatus.Valid;
        }

        return CatalogReferenceStatus.NotFound;
    }

    private static bool ReferenceBelongToAggregate(
        MealPlan plan,
        MealPlanStructureInput structure
    )
    {
        var meals = plan.Meals.ToDictionary(meal => meal.Id);
        foreach (var input in structure.Meals.Where(meal => meal.Id.HasValue))
        {
            if (!meals.TryGetValue(input.Id!.Value, out var meal))
                return false;

            var itemsIds = meal.Items.Select(item => item.Id).ToHashSet();
            var supplementsIds = meal.Supplements.Select(item => item.Id).ToHashSet();
            if (input.Items.Any(item => item.Id.HasValue && !itemsIds.Contains(item.Id!.Value))
                || input.Supplements.Any(item =>
                    item.Id.HasValue && !supplementsIds.Contains(item.Id!.Value)))
                return false;
        }

        return true;
    }

    private static void Reconcile(
        MealPlan plan,
        UpdateMealPlanWriteModel model,
        DateTime now
    )
    {
        plan.UpdateDetails(model.Name, model.Description, model.StartsDate, model.EndsDate, now);
        if (model.ReplacementCalculation is not null)
            plan.ReplaceCalculation(model.ReplacementCalculation, now);

        var desiredMealIds = model.Structure.Meals.Where(meal => meal.Id.HasValue)
            .Select(meal => meal.Id!.Value)
            .ToHashSet();
        foreach (var meal in plan.Meals.Where(meal => !desiredMealIds.Contains(meal.Id)).ToArray())
            plan.RemoveMeal(meal.Id, now);

        plan.ReorderMeals(
            model.Structure.Meals.Where(meal => meal.Id.HasValue)
                .ToDictionary(meal => meal.Id!.Value, meal => meal.OrderNumber),
            now
        );

        foreach (var input in model.Structure.Meals.Where(meal => meal.Id.HasValue))
            ReconcileExistingMeal(plan, input, now);

        foreach (var input in model.Structure.Meals.Where(meal => !meal.Id.HasValue)
            .OrderBy(meal => meal.OrderNumber))
        {
            var meal = plan.AddMeal(input.MealType, input.OrderNumber, now);
            AddChildren(meal, input, now);
        }
    }

    private static void ReconcileExistingMeal(
        MealPlan plan,
        MealPlanStructureInput.MealInput input,
        DateTime now
    )
    {
        var meal = plan.GetMeal(input.Id!.Value);
        var desiredItemIds = input.Items.Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();

        foreach (var item in meal.Items.Where(item => !desiredItemIds.Contains(item.Id)).ToArray())
            meal.RemoveItem(item.Id, now);

        meal.ReorderItems(
            input.Items.Where(item => item.Id.HasValue)
                .ToDictionary(item => item.Id!.Value, item => item.OrderNumber),
            now
        );

        foreach (var item in input.Items.Where(item => item.Id.HasValue))
            meal.UpdateItem(item.Id!.Value, item.FoodId, item.QuantityInGrams, item.OrderNumber, now);

        var desiredAssociationIds = input.Supplements.Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();

        foreach (var association in meal.Supplements
            .Where(item => !desiredAssociationIds.Contains(item.Id)).ToArray())
            meal.RemoveSupplement(association.Id, now);
        meal.ReorderSupplements(
            input.Supplements.Where(item => item.Id.HasValue)
                .ToDictionary(item => item.Id!.Value, item => item.OrderNumber),
            now
        );

        foreach (var item in input.Supplements.Where(item => item.Id.HasValue))
        {
            meal.UpdateSupplement(
                item.Id!.Value,
                item.SupplementId,
                item.Notes,
                item.Quantity,
                item.OrderNumber,
                now
            );
        }

        plan.UpdateMeal(meal.Id, input.MealType, input.OrderNumber, now);
        foreach (var item in input.Items.Where(item => !item.Id.HasValue)
            .OrderBy(item => item.OrderNumber))
            meal.AddItem(item.FoodId, item.QuantityInGrams, item.OrderNumber, now);
        foreach (var item in input.Supplements.Where(item => !item.Id.HasValue)
            .OrderBy(item => item.OrderNumber))
            meal.AddSupplement(
                item.SupplementId,
                item.Notes,
                item.Quantity,
                item.OrderNumber,
                now
            );
    }

    private static void AddNewStructure(
        MealPlan plan,
        MealPlanStructureInput structure,
        DateTime now
    )
    {
        foreach (var input in structure.Meals.OrderBy(meal => meal.OrderNumber))
        {
            var meal = plan.AddMeal(input.MealType, input.OrderNumber, now);
            AddChildren(meal, input, now);
        }
    }

    private static void AddChildren(
        MealPlanMeal meal,
        MealPlanStructureInput.MealInput input,
        DateTime now
    )
    {
        foreach (var item in input.Items.OrderBy(item => item.OrderNumber))
            meal.AddItem(item.FoodId, item.QuantityInGrams, item.OrderNumber, now);
        foreach (var item in input.Supplements.OrderBy(item => item.OrderNumber))
            meal.AddSupplement(
                item.SupplementId,
                item.Notes,
                item.Quantity,
                item.OrderNumber,
                now
            );
    }

    private static MealPlanStoreResult? MapCatalogFailure(CatalogReferenceStatus status) =>
        status switch
        {
            CatalogReferenceStatus.Valid => null,
            CatalogReferenceStatus.NotFound => MealPlanStoreResult.ForCatalogReferenceNotFound(),
            CatalogReferenceStatus.Inactive => MealPlanStoreResult.ForCatalogReferenceInactive(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    private static void ValidateArguments(Guid trainerId, object model)
    {
        if (trainerId == Guid.Empty)
            throw new ArgumentException("Trainer ID is required.", nameof(trainerId));
        ArgumentNullException.ThrowIfNull(model);
    }

    private enum CatalogReferenceStatus
    {
        Valid,
        NotFound,
        Inactive
    }
}
