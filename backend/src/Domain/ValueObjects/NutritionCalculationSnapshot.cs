using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Evidência imutável e versionada da origem dos alvos nutricionais de um plano.
/// As propriedades escalares tornam a representação JSON estável e auditável.
/// </summary>
public sealed class NutritionCalculationSnapshot
{
    public const int CurrentSchemaVersion = 1;
    public const string FormulaOrigin = "formula";
    public const string ManualEnergyOrigin = "manual_energy";

    public int SchemaVersion { get; private set; }
    public string CalculationOrigin { get; private set; } = null!;
    public DateTime CalculatedAt { get; private set; }

    public string? EnergyFormula { get; private set; }
    public decimal WeightKgUsed { get; private set; }
    public decimal? HeightCmUsed { get; private set; }
    public int? AgeUsed { get; private set; }
    public string? SexUsed { get; private set; }
    public decimal? BodyFatPercentageUsed { get; private set; }
    public string? ActivityLevel { get; private set; }
    public decimal? ActivityFactor { get; private set; }
    public string? GoalType { get; private set; }
    public decimal? GoalAdjustmentKcal { get; private set; }

    public decimal? RestingEnergyExpenditureKcal { get; private set; }
    public decimal? TotalDailyEnergyExpenditureKcal { get; private set; }
    public decimal TargetKcal { get; private set; }

    public MacroDistributionMode MacroDistributionMode { get; private set; }
    public decimal? ProteinPercentageInput { get; private set; }
    public decimal? CarbsPercentageInput { get; private set; }
    public decimal? FatsPercentageInput { get; private set; }
    public decimal? ProteinGramsPerKgInput { get; private set; }
    public decimal? FatsGramsPerKgInput { get; private set; }

    public decimal ProteinTargetGrams { get; private set; }
    public decimal CarbsTargetGrams { get; private set; }
    public decimal FatsTargetGrams { get; private set; }
    public decimal ProteinEnergyPercentage { get; private set; }
    public decimal CarbsEnergyPercentage { get; private set; }
    public decimal FatsEnergyPercentage { get; private set; }
    public decimal CalculatedMacroKcal { get; private set; }
    public decimal KcalDifference { get; private set; }

    private NutritionCalculationSnapshot() { }

    /// <summary>Cria um snapshot a partir de um fórmula energética executada.</summary>
    public static NutritionCalculationSnapshot FromFormula(
        EnergyCalculationResult energy,
        MacroCalculationResult macros,
        DateTime calculatedAt
    )
    {
        ArgumentNullException.ThrowIfNull(energy);
        ArgumentNullException.ThrowIfNull(macros);

        if (macros.TargetKcal != energy.TargetKcal)
            throw new DomainException("Macro and energy calculations must use the same target kcal.");
        if (macros.Mode == MacroDistributionMode.GramsPerKg &&
            macros.WeightUsedForMacros != energy.Input.WeightKg)
            throw new DomainException("Energy and grams-per-kg calculations must use the same weight.");

        return Create(
            FormulaOrigin,
            energy.Input.WeightKg,
            energy.Input.Formula.ToKey(),
            energy.Input.HeightCm,
            energy.Input.AgeUsed,
            energy.Input.SexUsed?.Value,
            energy.Input.BodyFatPercentage,
            energy.Input.ActivityLevel.Value,
            energy.Input.ActivityLevel.Factor,
            energy.Input.GoalType.ToKey(),
            energy.Input.GoalAdjustmentKcal,
            energy.RestingEnergyExpenditureKcal,
            energy.TotalDailyEnergyExpenditureKcal,
            macros,
            calculatedAt
        );
    }

    /// <summary>
    /// Cria um snapshot quando personal trainer define diretamente as kcal. O peso
    /// continua explícito para suportar o modo grams_per_kg e auditoria futura.
    /// </summary>
    public static NutritionCalculationSnapshot FromManualEnergy(
        decimal weightKgUsed,
        MacroCalculationResult macros,
        DateTime calculatedAt
    )
    {
        if (weightKgUsed <= 0)
            throw new DomainException("Weight used must be greater than zero.");

        ArgumentNullException.ThrowIfNull(macros);
        if (macros.Mode == MacroDistributionMode.GramsPerKg &&
            macros.WeightUsedForMacros != weightKgUsed)
            throw new DomainException("Snapshot and grams-per-kg calculation must use the same weight.");

        return Create(
            ManualEnergyOrigin,
            weightKgUsed,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            macros,
            calculatedAt
        );
    }

    private static NutritionCalculationSnapshot Create(
        string origin,
        decimal weightKgUsed,
        string? formula,
        decimal? heightCm,
        int? age,
        string? sex,
        decimal? bodyFatPercentage,
        string? activityLevel,
        decimal? activityFactor,
        string? goalType,
        decimal? goalAdjustmentKcal,
        decimal? restingEnergy,
        decimal? totalDailyEnergy,
        MacroCalculationResult macros,
        DateTime calculatedAt
    )
    {
        var targetKcal = Round(macros.TargetKcal);
        var proteinGrams = Round(macros.Targets.ProteinGrams);
        var carbsGrams = Round(macros.Targets.CarbsGrams);
        var fatsGrams = Round(macros.Targets.FatsGrams);
        var calculatedMacroKcal = Round(proteinGrams * 4m + carbsGrams * 4m + fatsGrams * 9m);
        var kcalDifference = Round(Math.Abs(calculatedMacroKcal - targetKcal));

        if (targetKcal <= 0)
            throw new DomainException("Rounded target kcal must be greater than zero.");
        if (kcalDifference > 100m)
            throw new DomainException("Rounded macro targets cannot differ from target kcal by more than 100 kcal.");

        return new NutritionCalculationSnapshot
        {
            SchemaVersion = CurrentSchemaVersion,
            CalculationOrigin = origin,
            CalculatedAt = calculatedAt,
            EnergyFormula = formula,
            WeightKgUsed = Round(weightKgUsed),
            HeightCmUsed = RoundNullable(heightCm),
            AgeUsed = age,
            SexUsed = sex,
            BodyFatPercentageUsed = RoundNullable(bodyFatPercentage),
            ActivityLevel = activityLevel,
            ActivityFactor = activityFactor,
            GoalType = goalType,
            GoalAdjustmentKcal = RoundNullable(goalAdjustmentKcal),
            RestingEnergyExpenditureKcal = RoundNullable(restingEnergy),
            TotalDailyEnergyExpenditureKcal = RoundNullable(totalDailyEnergy),
            TargetKcal = targetKcal,
            MacroDistributionMode = macros.Mode,
            ProteinPercentageInput = RoundNullable(macros.ProteinPercentageInput),
            CarbsPercentageInput = RoundNullable(macros.CarbsPercentageInput),
            FatsPercentageInput = RoundNullable(macros.FatsPercentageInput),
            ProteinGramsPerKgInput = RoundNullable(macros.ProteinGramsPerKgInput),
            FatsGramsPerKgInput = RoundNullable(macros.FatsGramsPerKgInput),
            ProteinTargetGrams = proteinGrams,
            CarbsTargetGrams = carbsGrams,
            FatsTargetGrams = fatsGrams,
            ProteinEnergyPercentage = Round(proteinGrams * 4m / calculatedMacroKcal * 100m),
            CarbsEnergyPercentage = Round(carbsGrams * 4m / calculatedMacroKcal * 100m),
            FatsEnergyPercentage = Round(fatsGrams * 9m / calculatedMacroKcal * 100m),
            CalculatedMacroKcal = calculatedMacroKcal,
            KcalDifference = kcalDifference
        };
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? RoundNullable(decimal? value) =>
        value.HasValue ? Round(value.Value) : null;
}
