namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Linha válida do CSV de alimentos, pronta a gravar. Valores por 100 g.</summary>
public sealed record GlobalFoodImportItem(
    int Line,
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber,
    decimal? DefaultServingGrams);
