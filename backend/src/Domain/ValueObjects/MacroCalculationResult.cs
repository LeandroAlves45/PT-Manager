namespace Domain.ValueObjects;

/// <summary>
/// Resultado não arredondado juntamente com os valores de configuração originais.
/// Os campos que não pertencem ao modo escolhido permanecem nulos.
/// </summary>
public sealed record MacroCalculationResult(
    MacroDistributionMode Mode,
    MacroSummary Targets,
    decimal TargetKcal,
    decimal KcalDifference,
    decimal? ProteinPercentageInput,
    decimal? CarbsPercentageInput,
    decimal? FatsPercentageInput,
    decimal? ProteinGramsPerKgInput,
    decimal? FatsGramsPerKgInput,
    decimal? WeightUsedForMacros
)
{
    public decimal ProteinEnergyPercentage => Targets.Kcal == 0
        ? 0
        : Targets.ProteinGrams * 4 / Targets.Kcal * 100;

    public decimal CarbsEnergyPercentage => Targets.Kcal == 0
        ? 0
        : Targets.CarbsGrams * 4 / Targets.Kcal * 100;

    public decimal FatsEnergyPercentage => Targets.Kcal == 0
        ? 0
        : Targets.FatsGrams * 9 / Targets.Kcal * 100;
}
