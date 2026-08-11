using Domain.ValueObjects;

namespace Application.Features.Nutrition.MealPlans.Abstractions;

/// <summary>Transporta a criação integral validada para uma transação.</summary>
public sealed record CreateMealPlanWriteModel(
    Guid ClientId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationSnapshot Calculation,
    MealPlanStructureInput Structure
);
