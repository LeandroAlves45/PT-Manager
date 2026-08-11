using Domain.Services;
using Domain.ValueObjects;

namespace Application.Features.Nutrition.Calculations;

/// <summary>Executa o cálculo nutricional server-side a partir de input validado.</summary>
public static class NutritionCalculationFactory
{
    /// <summary>
    /// Cria um snapshot versionado sem persistir nem consultar dependências externas.
    /// </summary>
    /// <param name="input">Input previamente validado.</param>
    /// <param name="calculatedAt">Instante UTC fornecido por IClock.</param>
    /// <returns>Snapshot imutável e coerente com os targets derivados.</returns>
    public static NutritionCalculationSnapshot CreateSnapshot(
        NutritionCalculationInput input,
        DateTime calculatedAt
    )
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));

        EnergyCalculationResult? energyResult;
        decimal targetKcal;

        if (input.CalculationOrigin == "formula")
        {
            var energyInput = new EnergyRequirementInput(
                ParseEnergyFormula(input.EnergyFormula),
                input.WeightKg,
                input.HeightCm,
                input.Age!.Value,
                input.Sex is null ? null : BiologicalSex.FromString(input.Sex),
                input.BodyFatPercentage,
                ActivityLevel.FromString(input.ActivityLevel!),
                ParseGoalType(input.GoalType),
                input.GoalAdjustmentKcal!.Value
            );

            energyResult = EnergyRequirementCalculator.Calculate(energyInput);
            targetKcal = energyResult.TargetKcal;
        }
        else if (input.CalculationOrigin == "manual_energy")
        {
            energyResult = null;
            targetKcal = input.ManualTargetKcal!.Value;
        }
        else
        {
            throw new InvalidOperationException(
                "Calculation origin violates the validated input contract.");
        }

        MacroCalculationResult macroResult = input.MacroMode switch
        {
            "percentage" => MacroTargetCalculator.CalculateFromPercentage(
                targetKcal,
                new PercentageMacroInput(
                    input.ProteinPercentage!.Value,
                    input.CarbsPercentage!.Value,
                    input.FatsPercentage!.Value
                )
            ),
            "grams_per_kg" => MacroTargetCalculator.CalculateFromGramsPerKg(
                targetKcal,
                input.WeightKg,
                new PerKgMacroInput(
                    input.ProteinGramsPerKg!.Value,
                    input.FatsGramsPerKg!.Value
                )
            ),
            "manual_grams" => MacroTargetCalculator.CalculateFromManualGrams(
                targetKcal,
                new ManualMacroInput(
                    input.ProteinGrams!.Value,
                    input.CarbsGrams!.Value,
                    input.FatsGrams!.Value
                )
            ),
            _ => throw new InvalidOperationException(
                "Macro mode violates the validated input contract.")
        };

        return energyResult is not null
            ? NutritionCalculationSnapshot.FromFormula(
                energyResult,
                macroResult,
                calculatedAt
            )
            : NutritionCalculationSnapshot.FromManualEnergy(
                input.WeightKg,
                macroResult,
                calculatedAt
            );
    }

    private static EnergyFormula ParseEnergyFormula(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "harris_benedict" => EnergyFormula.HarrisBenedict,
            "mifflin_st_jeor" => EnergyFormula.MifflinStJeor,
            "cunningham" => EnergyFormula.Cunningham,
            "tinsley" => EnergyFormula.Tinsley,
            _ => throw new InvalidOperationException(
                "Energy formula violates the validated input contract.")
        };

    private static NutritionGoalType ParseGoalType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "maintenance" => NutritionGoalType.Maintenance,
            "deficit" => NutritionGoalType.Deficit,
            "surplus" => NutritionGoalType.Surplus,
            _ => throw new InvalidOperationException(
                "Goal type violates the validated input contract.")
        };
}
