namespace Application.Features.Nutrition.Foods.ReactivateGlobalFood;

/// <summary>Identifica o alimento global a ser reativado.</summary>
public sealed record ReactivateGlobalFoodCommand(Guid FoodId);
