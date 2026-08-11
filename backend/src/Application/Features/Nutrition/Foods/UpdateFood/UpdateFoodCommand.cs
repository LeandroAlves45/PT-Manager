namespace Application.Features.Nutrition.Foods.UpdateFood;

/// <summary>Substitui os campos editáveis de um Food privado.</summary>
public sealed record UpdateFoodCommand(
    Guid FoodId,
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber
);
