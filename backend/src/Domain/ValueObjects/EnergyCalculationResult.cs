namespace Domain.ValueObjects;

/// <summary>Resultado não arredondado do cálculo energético.</summary>
public sealed record EnergyCalculationResult(
    EnergyRequirementInput Input,
    decimal RestingEnergyExpenditureKcal,
    decimal TotalDailyEnergyExpenditureKcal,
    decimal TargetKcal
);
