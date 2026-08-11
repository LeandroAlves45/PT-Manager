namespace Application.Features.Nutrition.MealPlans.ReactivateMealPlan;

/// <summary>Solicita a reativação idempotente de um MealPlan arquivado.</summary>
public sealed record ReactivateMealPlanCommand(Guid MealPlanId);
