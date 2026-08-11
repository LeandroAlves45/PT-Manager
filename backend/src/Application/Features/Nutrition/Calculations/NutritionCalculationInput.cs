namespace Application.Features.Nutrition.Calculations;

/// <summary>
/// Representa valores usados para calcular energia e macronutrientes no servidor.
/// </summary>
/// <param name="CalculationOrigin">fórmula ou manual_energy.</param>
/// <param name="EnergyFormula">Fórmula suportada quando a origem é formula.</param>
/// <param name="WeightKg">Peso efetivamente escolhido para o cálculo.</param>
/// <param name="HeightCm">Altura exigida pela fórmula selecionada.</param>
/// <param name="Age">Idade efetivamente usada, nunca derivada implicitamente.</param>
/// <param name="Sex">male ou female quando exigido.</param>
/// <param name="BodyFatPercentage">Percentagem de gordura quando exigida.</param>
/// <param name="ActivityLevel">Nível de atividade canónico.</param>
/// <param name="GoalType">maintenance, deficit ou surplus.</param>
/// <param name="GoalAdjustmentKcal">Magnitude positiva do ajuste.</param>
/// <param name="ManualTargetKcal">Target explícito para manual_energy.</param>
/// <param name="MacroMode">percentage, grams_per_kg ou manual_grams.</param>
/// <param name="ProteinPercentage">Percentagem de proteína.</param>
/// <param name="CarbsPercentage">Percentagem de hidratos.</param>
/// <param name="FatsPercentage">Percentagem de gordura.</param>
/// <param name="ProteinGramsPerKg">Proteína por kg.</param>
/// <param name="FatsGramsPerKg">Gordura por kg.</param>
/// <param name="ProteinGrams">Proteína manual em gramas.</param>
/// <param name="CarbsGrams">Hidratos manuais em gramas.</param>
/// <param name="FatsGrams">Gordura manual em gramas.</param>
public sealed record NutritionCalculationInput(
    string CalculationOrigin,
    string? EnergyFormula,
    decimal WeightKg,
    decimal? HeightCm,
    int? Age,
    string? Sex,
    decimal? BodyFatPercentage,
    string? ActivityLevel,
    string? GoalType,
    decimal? GoalAdjustmentKcal,
    decimal? ManualTargetKcal,
    string MacroMode,
    decimal? ProteinPercentage,
    decimal? CarbsPercentage,
    decimal? FatsPercentage,
    decimal? ProteinGramsPerKg,
    decimal? FatsGramsPerKg,
    decimal? ProteinGrams,
    decimal? CarbsGrams,
    decimal? FatsGrams
);
