using Application.Common.Abstractions;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Exceptions;
using Infrastructure.Data;
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

        if (_tenantContext.IsAdministrative)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var tenantId = context.RequireTenant();

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            // Procura pela propriedade de tenancy (OwnerTrainerId ou TrainerId)
            var ownerProperty = entry.Properties.FirstOrDefault(p =>
                p.Metadata.Name == "OwnerTrainerId" || p.Metadata.Name == "TrainerId");

            if (ownerProperty == null)
                continue; // Se não houver propriedade de tenancy, ignora a validação

            if (entry.State == EntityState.Added)
            {
                var currentValue = ownerProperty.CurrentValue as Guid?;

                // Atribui automaticamente se estiver vazio ou valida se for o tenant correto
                if (!currentValue.HasValue || currentValue.Value == Guid.Empty)
                {
                    ownerProperty.CurrentValue = tenantId;
                }
                else if (currentValue.Value != tenantId)
                {
                    throw new DomainException(
                        "Cannot create a record for another tenant."
                    );
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var originalValue = ownerProperty.OriginalValue as Guid?;
                var currentValue = ownerProperty.CurrentValue as Guid?;

                // Valida se o valor original e o valor atual correspondem ao tenant
                if (originalValue != tenantId || currentValue != tenantId)
                {
                    throw new DomainException(
                        "Tenant ownership cannot be changed."
                    );
                }
            }
        }

        await ValidateCatalogReferencesAsync(context, tenantId, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Verifica que itens, suplementos e exercícios referenciados são globais ou do
    /// tenant efetivo, e que um log de série pertence ao mesmo cliente do plano.
    /// </summary>
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
            .Distinct()
            .ToList();

        var referenceExerciseIds = context.ChangeTracker
            .Entries<TrainingPlanDayExercise>()
            .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
            .Select(entry => entry.Entity.ExerciseId)
            .Distinct()
            .ToList();

        //uma query por catálogo (não uma por linha). IgnoreQueryFilters
        // é necessário aqui: o alvo pode ser uma linha global (OwnerTrainerId
        // null) ou de outro personal trainer, e o Global Query Filter escondia-a — mas
        // precisamos de A VER para decidir se é legítima ou não, não para a
        // devolver ao chamador.
        if (referenceFoodIds.Count > 0)
        {
            var foods = await context.Foods
                .IgnoreQueryFilters()
                .Where(f => referenceFoodIds.Contains(f.Id))
                .Select(f => new { f.Id, f.OwnerTrainerId })
                .ToListAsync(cancellationToken);

            foreach (var food in foods)
            {
                if (food.OwnerTrainerId is not null && food.OwnerTrainerId != tenantId)
                {
                    throw new DomainException(
                        "Cannot reference a private catalog item from another personal trainer."
                    );
                }
            }
        }

        if (referenceSupplementIds.Count > 0)
        {
            var supplements = await context.Supplements
                .IgnoreQueryFilters()
                .Where(s => referenceSupplementIds.Contains(s.Id))
                .Select(s => new { s.Id, s.OwnerTrainerId })
                .ToListAsync(cancellationToken);

            foreach (var supplement in supplements)
            {
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
            var exercises = await context.Exercises
                .IgnoreQueryFilters()
                .Where(e => referenceExerciseIds.Contains(e.Id))
                .Select(e => new { e.Id, e.OwnerTrainerId })
                .ToListAsync(cancellationToken);

            foreach (var exercise in exercises)
            {
                if (exercise.OwnerTrainerId is not null && exercise.OwnerTrainerId != tenantId)
                {
                    throw new DomainException(
                        "Cannot reference a private catalog exercise from another personal trainer."
                    );
                }
            }
        }

        // ClientExerciseSetLog: o cliente do log tem de bater certo
        // com o cliente do TrainingPlan a que a série pertence.
        var logEntries = context.ChangeTracker
            .Entries<ClientExerciseSetLog>()
            .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();

        if (logEntries.Count > 0)
        {
            var dayExerciseIds = logEntries.Select(log => log.TrainingPlanDayExerciseId).Distinct().ToList();

            // Uma única query materializa toda a cadeia até TrainingPlan.
            var dayExercises = await context.TrainingPlanDayExercises
                .IgnoreQueryFilters()
                .Where(dx => dayExerciseIds.Contains(dx.Id))
                .Select(dx => new { dx.Id, dx.TrainingPlanDayId })
                .ToDictionaryAsync(x => x.Id, x => x.TrainingPlanDayId, cancellationToken);

            var daysIds = dayExercises.Values.Distinct().ToList();

            var days = await context.TrainingPlanDays
                .IgnoreQueryFilters()
                .Where(d => daysIds.Contains(d.Id))
                .Select(d => new { d.Id, d.TrainingPlanId })
                .ToDictionaryAsync(x => x.Id, x => x.TrainingPlanId, cancellationToken);

            var planIds = days.Values.Distinct().ToList();

            var plans = await context.TrainingPlans
                .IgnoreQueryFilters()
                .Where(tp => planIds.Contains(tp.Id))
                .Select(tp => new { tp.Id, tp.ClientId, tp.OwnerTrainerId, tp.IsDeleted })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var log in logEntries)
            {
                if (!dayExercises.TryGetValue(log.TrainingPlanDayExerciseId, out var trainingPlanDayId))
                    throw new DomainException(
                        "Referenced training plan day exercise does not exist."
                    );

                if (!days.TryGetValue(trainingPlanDayId, out var trainingPlanId))
                    throw new DomainException(
                        "Referenced training plan day does not exist."
                    );

                if (!plans.TryGetValue(trainingPlanId, out var plan))
                    throw new DomainException(
                        "Referenced training plan does not exist."
                    );

                if (plan.OwnerTrainerId != tenantId || plan.IsDeleted)
                    throw new DomainException(
                        "Cannot log against another tenant's plan."
                    );

                if (plan.ClientId != log.ClientId)
                    throw new DomainException(
                        "Log client does not match the training plan client."
                    );
            }
        }
    }
}
