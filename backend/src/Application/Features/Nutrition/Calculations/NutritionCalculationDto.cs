namespace Application.Features.Nutrition.Calculations;

/// <summary>Resultado canónico de um cálculo nutricional server-side.</summary>
public sealed record NutritionCalculationDto(
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
    decimal KcalDifference
);
