namespace Domain.ValueObjects;

/// <summary>
/// Fórmula de energia (kcal) baseada em macronutrientes: proteína, carboidrato e gordura.
/// Implementações de forma manual
/// </summary>
public sealed record ManualMacroInput(
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatsGrams
);
