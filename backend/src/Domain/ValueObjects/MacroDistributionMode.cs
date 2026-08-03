namespace Domain.ValueObjects;

/// <summary>Estratégia escolhida para distribuir as kcal pelos macronutrientes.</summary>
public enum MacroDistributionMode
{
    Percentage,
    GramsPerKg,
    ManualGrams
}

public static class MacroDistributionModeExtensions
{
    public static string ToKey(this MacroDistributionMode mode) => mode switch
    {
        MacroDistributionMode.Percentage => "percentage",
        MacroDistributionMode.GramsPerKg => "grams_per_kg",
        MacroDistributionMode.ManualGrams => "manual_grams",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
