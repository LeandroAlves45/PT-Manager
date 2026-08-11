namespace Application.Features.Nutrition.MealPlans.Dtos;

/// <summary>Agrega energia e nutrientes efetivos calculados a partir de Foods.</summary>
public sealed record NutritionTotalsDto(
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatsGrams,
    decimal Kcal,
    decimal FiberGrams
)
{
    /// <summary>Representa totais nulos sem significado de dados em falta.</summary>
    public static NutritionTotalsDto Zero { get; } = new(0m, 0m, 0m, 0m, 0m);
}

