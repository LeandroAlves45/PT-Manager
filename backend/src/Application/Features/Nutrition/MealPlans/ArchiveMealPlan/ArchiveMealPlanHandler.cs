using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.MealPlans.ArchiveMealPlan;

/// <summary>Arquiva um plano alimentar de forma idempotente.</summary>
public sealed class ArchiveMealPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMealPlanStore _mealPlanStore;

    public ArchiveMealPlanHandler(
        ITenantContext tenantContext,
        IClock clock,
        IMealPlanStore mealPlanStore
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(mealPlanStore);
        _tenantContext = tenantContext;
        _clock = clock;
        _mealPlanStore = mealPlanStore;
    }

    public async Task<Result> HandleAsync(
        ArchiveMealPlanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.MealPlanId == Guid.Empty)
            return Result.Failure(CreateMealPlanIdRequiredError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result.Failure(tenant.Error!);

        var outcome = await _mealPlanStore.SetArchivedAsync(
            command.MealPlanId,
            tenant.Value,
            true,
            _clock.UtcNow,
            cancellationToken
        );
        return MapTransition(outcome);
    }

    private static Result MapTransition(MealPlanStoreResult outcome) =>
        outcome.Kind switch
        {
            MealPlanStoreResult.Status.Changed => Result.Success(),
            MealPlanStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            MealPlanStoreResult.Status.NotFound => Result.Failure(NutritionErrors.MealPlanNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    private static Error CreateMealPlanIdRequiredError() => Error.Validation([
        new ValidationError(
            "MealPlanId",
            "meal_plan_id_required",
            "Meal plan ID is required."
        )
    ]);
}
