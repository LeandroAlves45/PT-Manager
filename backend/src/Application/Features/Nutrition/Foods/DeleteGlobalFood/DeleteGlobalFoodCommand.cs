namespace Application.Features.Nutrition.Foods.DeleteGlobalFood;

/// <summary>Identifica o alimento global a ser eliminado fisicamente.</summary>
public sealed record DeleteGlobalFoodCommand(Guid FoodId);
