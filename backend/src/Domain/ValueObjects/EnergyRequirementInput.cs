namespace Domain.ValueObjects;

/// <summary>Valores explícitos que o personal trainer decidiu usar no cálculo energético.</summary>
public sealed record EnergyRequirementInput(
    EnergyFormula Formula,
    decimal WeightKg,
    decimal? HeightCm,
    int AgeUsed,
    BiologicalSex? SexUsed,
    decimal? BodyFatPercentage,
    ActivityLevel ActivityLevel,
    NutritionGoalType GoalType,
    decimal GoalAdjustmentKcal
);
