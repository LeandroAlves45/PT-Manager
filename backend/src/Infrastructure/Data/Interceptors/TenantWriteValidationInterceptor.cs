using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Notifications;
using Domain.Entities.Nutrition;
using Domain.Entities.Sessions;
using Domain.Entities.Supplements;
using Domain.Entities.TrainerSettings;
using Domain.Entities.Training;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Data.Interceptors;

/// <summary>
/// Interceptor que valida as operações de escrita para garantir que o
/// TenantId seja consistente com o contexto atual.
/// </summary>
public sealed class TenantWriteValidationInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantWriteValidationInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Rejeita explicitamente chamadas síncronas para impedir que
    /// a validação assíncrona seja contornada.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        throw new InvalidOperationException(
            "Use SaveChangesAsync so tenant validation cannot be bypassed.");
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not PtManagerDbContext context)
            throw new InvalidOperationException("DbContext is not of type PtManagerDbContext.");

        var entries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        Guid? tenantId = null;

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                // Política A: ownership obrigatório e soft delete
                case Client:
                case MealPlan:
                case TrainingPlan:
                case Session:
                case CheckIn:
                case InitialAssessment:
                case ClientSessionPack:
                case Notification:
                case ClientSupplementAssignment:
                case PackType:
                    tenantId ??= context.RequireTenant();
                    ValidateRequiredOwnership(entry, "OwnerTrainerId", tenantId.Value);
                    break;

                // Política A': ownership obrigatório sem soft delete
                case TrainerSettings:
                case TrainerSubscription:
                    tenantId ??= context.RequireTenant();
                    ValidateRequiredOwnership(entry, "TrainerId", tenantId.Value);
                    break;

                // Política B: null representa código global e só pode ser escrito
                // através de uma operação administrativa já autorizada.
                case Food:
                case Exercise:
                case Supplement:
                    ValidateCatalogOwnership(entry, context, ref tenantId);
                    break;
            }
        }

        // Referências de agregados tenant-owned exigem sempre um tenant efetivo
        if (HasTenantScopedReferences(context))
        {
            tenantId ??= context.RequireTenant();
            await ValidateDerivedAggregateOwnershipAsync(
                context,
                tenantId.Value,
                cancellationToken);
            await ValidateCatalogReferencesAsync(context, tenantId.Value, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Confirma que cada entidade filha pertence a uma raiz do tenant efetivo.
    /// A validação considera também agregados novos ainda não persistidos.
    /// </summary>
    private static async Task ValidateDerivedAggregateOwnershipAsync(
        PtManagerDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await ValidateMealPlanOwnershipAsync(context, tenantId, cancellationToken);
        await ValidateTrainingPlanOwnershipAsync(context, tenantId, cancellationToken);
    }

    private static async Task ValidateMealPlanOwnershipAsync(
        PtManagerDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var changedMeals = context.ChangeTracker.Entries<MealPlanMeal>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();

        var mealIds = changedMeals.Select(meal => meal.Id)
            .Concat(context.ChangeTracker.Entries<MealPlanMealItem>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity.MealPlanMealId))
            .Concat(context.ChangeTracker.Entries<MealPlanMealSupplement>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity.MealPlanMealId))
            .Distinct()
            .ToList();

        if (mealIds.Count == 0)
            return;

        var meals = context.ChangeTracker.Entries<MealPlanMeal>()
            .Select(entry => entry.Entity)
            .Where(meal => mealIds.Contains(meal.Id))
            .ToDictionary(meal => meal.Id);

        var missingMealIds = mealIds.Where(id => !meals.ContainsKey(id)).ToList();
        if (missingMealIds.Count > 0)
        {
            var persistedMeals = await context.MealPlanMeals
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(meal => missingMealIds.Contains(meal.Id))
                .ToListAsync(cancellationToken);

            foreach (var meal in persistedMeals)
                meals.Add(meal.Id, meal);
        }

        foreach (var mealId in mealIds)
        {
            if (!meals.ContainsKey(mealId))
                throw new DomainException("Referenced meal does not exist.");
        }

        var planIds = meals.Values
            .Select(meal => meal.MealPlanId)
            .Distinct()
            .ToList();

        var plans = context.ChangeTracker.Entries<MealPlan>()
            .Select(entry => entry.Entity)
            .Where(plan => planIds.Contains(plan.Id))
            .ToDictionary(plan => plan.Id);

        var missingPlanIds = planIds.Where(id => !plans.ContainsKey(id)).ToList();
        if (missingPlanIds.Count > 0)
        {
            var persistedPlans = await context.MealPlans
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(plan => missingPlanIds.Contains(plan.Id))
                .ToListAsync(cancellationToken);

            foreach (var plan in persistedPlans)
                plans.Add(plan.Id, plan);
        }

        foreach (var meal in meals.Values)
        {
            if (!plans.TryGetValue(meal.MealPlanId, out var plan))
                throw new DomainException("Referenced meal plan does not exist.");

            if (plan.OwnerTrainerId != tenantId || plan.IsDeleted)
                throw new DomainException("Cannot write to another tenant's meal plan.");
        }
    }

    private static async Task ValidateTrainingPlanOwnershipAsync(
        PtManagerDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var changedDays = context.ChangeTracker.Entries<TrainingPlanDay>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();

        var changedDayExercises = context.ChangeTracker.Entries<TrainingPlanDayExercise>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();

        var changedLogs = context.ChangeTracker.Entries<ClientExerciseSetLog>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();

        var dayExerciseIds = changedDayExercises.Select(dayExercise => dayExercise.Id)
            .Concat(context.ChangeTracker.Entries<ExerciseSet>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry => entry.Entity.TrainingPlanDayExerciseId))
            .Concat(changedLogs.Select(log => log.TrainingPlanDayExerciseId))
            .Distinct()
            .ToList();

        var dayExercises = context.ChangeTracker.Entries<TrainingPlanDayExercise>()
            .Select(entry => entry.Entity)
            .Where(dayExercise => dayExerciseIds.Contains(dayExercise.Id))
            .ToDictionary(dayExercise => dayExercise.Id);

        var missingDayExerciseIds = dayExerciseIds
            .Where(id => !dayExercises.ContainsKey(id))
            .ToList();

        if (missingDayExerciseIds.Count > 0)
        {
            var persistedDayExercises = await context.TrainingPlanDayExercises
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(dayExercise => missingDayExerciseIds.Contains(dayExercise.Id))
                .ToListAsync(cancellationToken);

            foreach (var dayExercise in persistedDayExercises)
                dayExercises.Add(dayExercise.Id, dayExercise);
        }

        foreach (var dayExerciseId in dayExerciseIds)
        {
            if (!dayExercises.ContainsKey(dayExerciseId))
                throw new DomainException("Referenced training plan day exercise does not exist.");
        }

        var dayIds = changedDays.Select(day => day.Id)
            .Concat(dayExercises.Values.Select(dayExercise => dayExercise.TrainingPlanDayId))
            .Distinct()
            .ToList();

        if (dayIds.Count == 0)
            return;

        var days = context.ChangeTracker.Entries<TrainingPlanDay>()
            .Select(entry => entry.Entity)
            .Where(day => dayIds.Contains(day.Id))
            .ToDictionary(day => day.Id);

        var missingDayIds = dayIds.Where(id => !days.ContainsKey(id)).ToList();
        if (missingDayIds.Count > 0)
        {
            var persistedDays = await context.TrainingPlanDays
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(day => missingDayIds.Contains(day.Id))
                .ToListAsync(cancellationToken);

            foreach (var day in persistedDays)
                days.Add(day.Id, day);
        }

        foreach (var dayId in dayIds)
        {
            if (!days.ContainsKey(dayId))
                throw new DomainException("Referenced training plan day does not exist.");
        }

        var planIds = days.Values.Select(day => day.TrainingPlanId).Distinct().ToList();
        var plans = context.ChangeTracker.Entries<TrainingPlan>()
            .Select(entry => entry.Entity)
            .Where(plan => planIds.Contains(plan.Id))
            .ToDictionary(plan => plan.Id);

        var missingPlanIds = planIds.Where(id => !plans.ContainsKey(id)).ToList();
        if (missingPlanIds.Count > 0)
        {
            var persistedPlans = await context.TrainingPlans
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(plan => missingPlanIds.Contains(plan.Id))
                .ToListAsync(cancellationToken);

            foreach (var plan in persistedPlans)
                plans.Add(plan.Id, plan);
        }

        foreach (var day in days.Values)
        {
            if (!plans.TryGetValue(day.TrainingPlanId, out var plan))
                throw new DomainException("Referenced training plan does not exist.");

            if (plan.OwnerTrainerId != tenantId || plan.IsDeleted)
                throw new DomainException("Cannot write to another tenant's training plan.");
        }

        foreach (var log in changedLogs)
        {
            var dayExercise = dayExercises[log.TrainingPlanDayExerciseId];
            var day = days[dayExercise.TrainingPlanDayId];
            var plan = plans[day.TrainingPlanId];

            if (plan.ClientId != log.ClientId)
                throw new DomainException("Log client does not match the training plan client.");
        }
    }

    private async Task ValidateCatalogReferencesAsync(
        PtManagerDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Recolher ids do catálogo
        var referenceFoodIds = context.ChangeTracker
            .Entries<MealPlanMealItem>()
            .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
            .Select(entry => entry.Entity.FoodId)
            .Distinct()
            .ToList();

        var referenceSupplementIds = context.ChangeTracker
            .Entries<MealPlanMealSupplement>()
            .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
            .Select(entry => entry.Entity.SupplementId)
            .Concat(context.ChangeTracker.Entries<ClientSupplementAssignment>()
                .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
                .Select(entry => entry.Entity.SupplementId))
            .Distinct()
            .ToList();

        var referenceExerciseIds = context.ChangeTracker
            .Entries<TrainingPlanDayExercise>()
            .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
            .Select(entry => entry.Entity.ExerciseId)
            .Distinct()
            .ToList();

        // Uma query por catálogo (não uma por linha). IgnoreQueryFilters
        // é necessário aqui: o alvo pode ser uma linha global (OwnerTrainerId
        // null) ou de outro personal trainer, e o Global Query Filter escondia-a — mas
        // precisamos de A VER para decidir se é legítima ou não, não para a
        // devolver ao chamador.
        if (referenceFoodIds.Count > 0)
        {
            var foods = context.ChangeTracker.Entries<Food>()
                .Where(entry => referenceFoodIds.Contains(entry.Entity.Id))
                .Select(entry => new
                {
                    entry.Entity.Id,
                    entry.Entity.OwnerTrainerId,
                    entry.Entity.IsDeleted,
                })
                .ToDictionary(food => food.Id);

            var missingFoodIds = referenceFoodIds
                .Where(id => !foods.ContainsKey(id))
                .ToList();

            if (missingFoodIds.Count > 0)
            {
                // Os filtros são ignorados apenas para classificar corretamente a referência.
                var persistedFoods = await context.Foods
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(food => missingFoodIds.Contains(food.Id))
                    .Select(food => new
                    {
                        food.Id,
                        food.OwnerTrainerId,
                        food.IsDeleted,
                    })
                    .ToListAsync(cancellationToken);

                foreach (var food in persistedFoods)
                    foods.Add(food.Id, food);
            }

            foreach (var foodId in referenceFoodIds)
            {
                if (!foods.TryGetValue(foodId, out var food))
                    throw new DomainException("Referenced catalog food does not exist.");

                if (food.IsDeleted)
                    throw new DomainException("Cannot reference a deleted catalog food.");

                if (food.OwnerTrainerId is not null && food.OwnerTrainerId != tenantId)
                    throw new DomainException(
                        "Cannot reference a private catalog food from another personal trainer."
                    );
            }
        }

        if (referenceSupplementIds.Count > 0)
        {
            var supplements = context.ChangeTracker.Entries<Supplement>()
                .Where(entry => referenceSupplementIds.Contains(entry.Entity.Id))
                .Select(entry => new
                {
                    entry.Entity.Id,
                    entry.Entity.OwnerTrainerId,
                    entry.Entity.IsDeleted,
                })
                .ToDictionary(supplement => supplement.Id);

            var missingSupplementIds = referenceSupplementIds
                .Where(id => !supplements.ContainsKey(id))
                .ToList();

            if (missingSupplementIds.Count > 0)
            {
                // IgnoreQueryFilters é necessário para distinguir uma referência
                // apagada ou cross-tenant de uma referência que não existe.
                var persistedSupplements = await context.Supplements
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(supplement => missingSupplementIds.Contains(supplement.Id))
                    .Select(supplement => new
                    {
                        supplement.Id,
                        supplement.OwnerTrainerId,
                        supplement.IsDeleted,
                    })
                    .ToListAsync(cancellationToken);

                foreach (var supplement in persistedSupplements)
                    supplements.Add(supplement.Id, supplement);
            }

            foreach (var supplementId in referenceSupplementIds)
            {
                if (!supplements.TryGetValue(supplementId, out var supplement))
                    throw new DomainException("Referenced catalog supplement does not exist.");

                if (supplement.IsDeleted)
                    throw new DomainException("Cannot reference a deleted catalog supplement.");

                if (supplement.OwnerTrainerId is not null && supplement.OwnerTrainerId != tenantId)
                {
                    throw new DomainException(
                        "Cannot reference a private catalog supplement from another personal trainer."
                    );
                }
            }
        }

        if (referenceExerciseIds.Count > 0)
        {
            var exercises = context.ChangeTracker.Entries<Exercise>()
                .Where(entry => referenceExerciseIds.Contains(entry.Entity.Id))
                .Select(entry => new
                {
                    entry.Entity.Id,
                    entry.Entity.OwnerTrainerId,
                    entry.Entity.IsDeleted,
                })
                .ToDictionary(exercise => exercise.Id);

            var missingExerciseIds = referenceExerciseIds
                .Where(id => !exercises.ContainsKey(id))
                .ToList();

            if (missingExerciseIds.Count > 0)
            {
                var persistedExercises = await context.Exercises
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(exercise => missingExerciseIds.Contains(exercise.Id))
                    .Select(exercise => new
                    {
                        exercise.Id,
                        exercise.OwnerTrainerId,
                        exercise.IsDeleted,
                    })
                    .ToListAsync(cancellationToken);

                foreach (var exercise in persistedExercises)
                    exercises.Add(exercise.Id, exercise);
            }

            foreach (var exerciseId in referenceExerciseIds)
            {
                if (!exercises.TryGetValue(exerciseId, out var exercise))
                    throw new DomainException("Referenced catalog exercise does not exist.");

                if (exercise.IsDeleted)
                    throw new DomainException("Cannot reference a deleted catalog exercise.");

                if (exercise.OwnerTrainerId is not null && exercise.OwnerTrainerId != tenantId)
                    throw new DomainException(
                        "Cannot reference a private catalog exercise from another personal trainer."
                    );
            }
        }
    }

    private static void ValidateRequiredOwnership(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string propertyName,
        Guid tenantId)
    {
        var property = entry.Property(propertyName);
        var currentValue = (Guid?)property.CurrentValue;

        if (entry.State == EntityState.Added)
        {
            if (!currentValue.HasValue || currentValue.Value == Guid.Empty)
            {
                property.CurrentValue = tenantId;
                return;
            }

            if (currentValue.Value != tenantId)
                throw new DomainException("Cannot create a record for another tenant.");

            return;
        }

        var originalValue = (Guid?)property.OriginalValue;
        if (originalValue != tenantId || currentValue != tenantId)
            throw new DomainException("Tenant ownership cannot be changed.");
    }

    private void ValidateCatalogOwnership(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        PtManagerDbContext context,
        ref Guid? tenantId)
    {
        var property = entry.Property("OwnerTrainerId");
        var currentValue = (Guid?)property.CurrentValue;
        var originalValue = entry.State == EntityState.Modified
            ? (Guid?)property.OriginalValue
            : null;

        if (entry.State == EntityState.Modified && originalValue != currentValue)
            throw new DomainException("Catalog ownership cannot be changed.");

        if (!currentValue.HasValue)
        {
            if (!_tenantContext.IsAdministrative)
                throw new DomainException("Only an administrative operation can write global catalog items.");

            return;
        }

        tenantId ??= context.RequireTenant();

        if (currentValue != tenantId)
            throw new DomainException("Cannot write a private catalog item for another tenant.");
    }

    private static bool HasTenantScopedReferences(PtManagerDbContext context) =>
        context.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified &&
            entry.Entity is (
                MealPlanMeal
                or MealPlanMealItem
                or MealPlanMealSupplement
                or TrainingPlanDay
                or TrainingPlanDayExercise
                or ExerciseSet
                or ClientExerciseSetLog
                or ClientSupplementAssignment
            ));
}
