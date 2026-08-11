namespace Application.Features.Nutrition.Foods.CreateFood;

/// <summary>Solicita a criação de um alimento privado com valores por 100g.</summary>
public sealed record CreateFoodCommand(
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber
);
