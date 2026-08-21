namespace Application.Features.Nutrition.Foods.UpdateGlobalFood;

/// <summary>Dados editáveis de um alimento global existente.</summary>
public sealed record UpdateGlobalFoodCommand(
    Guid FoodId,
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber
);
