using Application.Features.Nutrition.Calculations;

namespace Api.Contracts.Nutrition;


/// <summary>Entrada de cálculo de energia e macronutrientes feitos no servidor.</summary>
public sealed record NutritionCalculationRequest(
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
    decimal? FatsGrams)
{
    /// <summary>Converte o contrato da API na entrada da Application.</summary>
    public NutritionCalculationInput ToInput() =>
        new(
            CalculationOrigin,
            EnergyFormula,
            WeightKg,
            HeightCm,
            Age,
            Sex,
            BodyFatPercentage,
            ActivityLevel,
            GoalType,
            GoalAdjustmentKcal,
            ManualTargetKcal,
            MacroMode,
            ProteinPercentage,
            CarbsPercentage,
            FatsPercentage,
            ProteinGramsPerKg,
            FatsGramsPerKg,
            ProteinGrams,
            CarbsGrams,
            FatsGrams
        );
}

/// <summary>Resultado canónico do cálculo nutricional.</summary>
public sealed record NutritionCalculationResponse(
    int SchemaVersion,
    string CalculationOrigin,
    DateTime CalculatedAt,
    string? EnergyFormula,
    decimal WeightKgUsed,
    decimal? HeightCmUsed,
    int? AgeUsed,
    string? SexUsed,
    decimal? BodyFatPercentageUsed,
    string? ActivityLevel,
    decimal? ActivityFactor,
    string? GoalType,
    decimal? GoalAdjustmentKcal,
    decimal? RestingEnergyExpenditureKcal,
    decimal? TotalDailyEnergyExpenditureKcal,
    decimal TargetKcal,
    string MacroDistributionMode,
    decimal ProteinTargetGrams,
    decimal CarbsTargetGrams,
    decimal FatsTargetGrams,
    decimal ProteinEnergyPercentage,
    decimal CarbsEnergyPercentage,
    decimal FatsEnergyPercentage,
    decimal CalculatedMacroKcal,
    decimal KcalDifference)
{
    /// <summary>Projeta o resultado da Application no contrato da Api.</summary>
    public static NutritionCalculationResponse From(NutritionCalculationDto calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        return new(
            calculation.SchemaVersion,
            calculation.CalculationOrigin,
            calculation.CalculatedAt,
            calculation.EnergyFormula,
            calculation.WeightKgUsed,
            calculation.HeightCmUsed,
            calculation.AgeUsed,
            calculation.SexUsed,
            calculation.BodyFatPercentageUsed,
            calculation.ActivityLevel,
            calculation.ActivityFactor,
            calculation.GoalType,
            calculation.GoalAdjustmentKcal,
            calculation.RestingEnergyExpenditureKcal,
            calculation.TotalDailyEnergyExpenditureKcal,
            calculation.TargetKcal,
            calculation.MacroDistributionMode,
            calculation.ProteinTargetGrams,
            calculation.CarbsTargetGrams,
            calculation.FatsTargetGrams,
            calculation.ProteinEnergyPercentage,
            calculation.CarbsEnergyPercentage,
            calculation.FatsEnergyPercentage,
            calculation.CalculatedMacroKcal,
            calculation.KcalDifference);
    }
}

