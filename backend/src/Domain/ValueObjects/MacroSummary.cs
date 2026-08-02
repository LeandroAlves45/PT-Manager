using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Resumo de macros em gramas (proteína, hidratos, gordura) com as kcal
/// derivadas pela fórmula de Atwater: protein * 4 + carbs * 4 + fats * 9.
/// </summary>
public sealed record MacroSummary
{
    public decimal ProteinGrams { get; }
    public decimal CarbsGrams { get; }
    public decimal FatsGrams { get; }
    public decimal Kcal => ProteinGrams * 4 + CarbsGrams * 4 + FatsGrams * 9;

    /// <summary>Cria um resumo de macros não negativos.</summary>
    public MacroSummary(decimal proteinGrams, decimal carbsGrams, decimal fatsGrams)
    {
        if (proteinGrams < 0 || carbsGrams < 0 || fatsGrams < 0)
            throw new DomainException("Macro grams cannot be negative.");

        ProteinGrams = proteinGrams;
        CarbsGrams = carbsGrams;
        FatsGrams = fatsGrams;
    }
}
