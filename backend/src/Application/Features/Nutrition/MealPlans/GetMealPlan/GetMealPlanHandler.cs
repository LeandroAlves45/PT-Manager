using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Results;

namespace Application.Features.Nutrition.MealPlans.GetMealPlan;

/// <summary>Obtém um plano alimentar completo do tenant efetivo.</summary>
public sealed class GetMealPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IMealPlanQueries _mealPlanQueries;

    public GetMealPlanHandler(
        ITenantContext tenantContext,
        IMealPlanQueries mealPlanQueries
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(mealPlanQueries);
        _tenantContext = tenantContext;
        _mealPlanQueries = mealPlanQueries;
    }

    public async Task<Result<MealPlanDetailsDto>> HandleAsync(
        GetMealPlanQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.MealPlanId == Guid.Empty)
        {
            return Result<MealPlanDetailsDto>.Failure(Error.Validation([
                new ValidationError(
                    "MealPlanId",
                    "meal_plan_id_required",
                    "Meal plan ID is required."
                )
            ]));
        }

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<MealPlanDetailsDto>.Failure(tenant.Error!);

        var details = await _mealPlanQueries.GetDetailsAsync(query.MealPlanId, cancellationToken);

        return details is null
            ? Result<MealPlanDetailsDto>.Failure(NutritionErrors.MealPlanNotFound)
            : Result<MealPlanDetailsDto>.Success(details);
    }
}
