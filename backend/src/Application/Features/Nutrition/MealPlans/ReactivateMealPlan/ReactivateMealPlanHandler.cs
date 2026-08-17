using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.MealPlans.ReactivateMealPlan;

/// <summary>Reativa um plano alimentar de forma idempotente.</summary>
public sealed class ReactivateMealPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMealPlanStore _mealPlanStore;

    public ReactivateMealPlanHandler(
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
        ReactivateMealPlanCommand command,
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
            false,
            _clock.UtcNow,
            cancellationToken
        );
        return outcome.ToTransitionResult();
    }

    private static Error CreateMealPlanIdRequiredError() => Error.Validation([
        new ValidationError(
            "MealPlanId",
            "meal_plan_id_required",
            "Meal plan ID is required."
        )
    ]);
}
