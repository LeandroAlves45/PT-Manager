namespace Domain.ValueObjects;

/// <summary>
/// Valores em porcentagem de macronutrientes: proteína, carboidrato e gordura.
/// </summary>
public sealed record PercentageMacroInput(
    decimal ProteinPercentage,
    decimal CarbsPercentage,
    decimal FatsPercentage
);
