namespace Application.Features.Nutrition.MealPlans.ListMealPlans;

/// <summary>Solicita uma página de MealPlans com filtros estáveis.</summary>
public sealed record ListMealPlansQuery(
    Guid? ClientId,
    string? Search,
    MealPlanActivityFilter Activity = MealPlanActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
