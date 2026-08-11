using Application.Features.Nutrition.Calculations;

namespace Application.Features.Nutrition.PreviewNutrition;

/// <summary>Solicita preview nutricional sem persistência.</summary>
public sealed record PreviewNutritionCommand(NutritionCalculationInput Calculation);
