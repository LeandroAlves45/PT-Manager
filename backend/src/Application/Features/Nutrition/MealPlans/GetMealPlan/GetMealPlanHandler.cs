using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _mealPlanQueries = mealPlanQueries ?? throw new ArgumentNullException(nameof(mealPlanQueries));
    }

    public async Task<Result<MealPlanDetailsDto>> HandleAsync(
        GetMealPlanQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.MealPlanId == Guid.Empty)
            return Result<MealPlanDetailsDto>.Failure(NutritionErrors.MealPlanIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.MealPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result<MealPlanDetailsDto>.Failure(actor.Error!);

        var details = await _mealPlanQueries.GetDetailsAsync(query.MealPlanId, cancellationToken);

        return details is null
            ? Result<MealPlanDetailsDto>.Failure(NutritionErrors.MealPlanNotFound)
            : Result<MealPlanDetailsDto>.Success(details);
    }
}
