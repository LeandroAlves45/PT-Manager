namespace Application.Features.Nutrition.Foods.ReactivateFood;

/// <summary>Solicita a reativação idempotente de um alimento privado.</summary>
public sealed record ReactivateFoodCommand(Guid FoodId);
