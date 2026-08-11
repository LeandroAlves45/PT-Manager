using Application.Features.Nutrition.Calculations;

namespace Application.Features.Nutrition.MealPlans.UpdateMealPlan;

/// <summary>Reconcilia um MealPlan existente sem criar outro plano.</summary>
public sealed record UpdateMealPlanCommand(
    Guid MealPlanId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationInput? Calculation,
    MealPlanStructureInput Structure
);
