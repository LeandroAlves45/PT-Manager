namespace Application.Features.Nutrition.MealPlans.GetMealPlan;

/// <summary>Solicita um MealPlan completo com tenant efetivo.</summary>
public sealed record GetMealPlanQuery(Guid MealPlanId);
