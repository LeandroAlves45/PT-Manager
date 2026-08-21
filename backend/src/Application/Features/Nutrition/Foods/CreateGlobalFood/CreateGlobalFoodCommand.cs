namespace Application.Features.Nutrition.Foods.CreateGlobalFood;

/// <summary>Dados editáveis de um novo alimento global.</summary>
public sealed record CreateGlobalFoodCommand(
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber
);
