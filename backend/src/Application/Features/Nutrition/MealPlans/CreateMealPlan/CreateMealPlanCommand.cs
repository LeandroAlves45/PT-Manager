using Application.Features.Nutrition.Calculations;

namespace Application.Features.Nutrition.MealPlans.CreateMealPlan;

/// <summary>Solicita um plano alimentar e a sua árvore inicial.</summary>
public sealed record CreateMealPlanCommand(
    Guid ClientId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationInput Calculation,
    MealPlanStructureInput Structure
);
