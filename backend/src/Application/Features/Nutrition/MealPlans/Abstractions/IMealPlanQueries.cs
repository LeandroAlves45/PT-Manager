using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Features.Nutrition.MealPlans.ListMealPlans;
using Application.Pagination;

namespace Application.Features.Nutrition.MealPlans.Abstractions;

/// <summary>Executa projeções read-only de planos alimentares.</summary>
public interface IMealPlanQueries
{
    Task<MealPlanDetailsDto?> GetDetailsAsync(
        Guid mealPlanId,
        CancellationToken cancellationToken
    );

    Task<PageResult<MealPlanSummaryDto>> ListAsync(
        Guid? clientId,
        string? search,
        MealPlanActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
