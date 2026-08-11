using Domain.ValueObjects;

namespace Application.Features.Nutrition.MealPlans.Abstractions;

/// <summary>Transporta a árvore final e substituição opcional do cálculo.</summary>
public sealed record UpdateMealPlanWriteModel(
    Guid MealPlanId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationSnapshot? ReplacementCalculation,
    MealPlanStructureInput Structure
);
