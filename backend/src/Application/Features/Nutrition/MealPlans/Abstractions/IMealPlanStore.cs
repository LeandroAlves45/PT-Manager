namespace Application.Features.Nutrition.MealPlans.Abstractions;

/// <summary>Executa escritas compostas do agregado MealPlan.</summary>
public interface IMealPlanStore
{
    Task<MealPlanStoreResult> CreateAsync(
        Guid trainerId,
        CreateMealPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<MealPlanStoreResult> UpdateAsync(
        Guid trainerId,
        UpdateMealPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<MealPlanStoreResult> SetArchivedAsync(
        Guid mealPlanId,
        Guid trainerId,
        bool isArchived,
        DateTime now,
        CancellationToken cancellationToken
    );
}
