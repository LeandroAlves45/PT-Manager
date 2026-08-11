namespace Application.Features.Nutrition.MealPlans.ArchiveMealPlan;

/// <summary>Solicita o arquivo idempotente de um MealPlan.</summary>
public sealed record ArchiveMealPlanCommand(Guid MealPlanId);
