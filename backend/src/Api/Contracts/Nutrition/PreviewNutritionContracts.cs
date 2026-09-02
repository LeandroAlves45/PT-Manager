namespace Api.Contracts.Nutrition;

/// <summary>Pedido de cálculo nutricional sem persistência.</summary>
public sealed record PreviewNutritionRequest(NutritionCalculationRequest Calculation);
