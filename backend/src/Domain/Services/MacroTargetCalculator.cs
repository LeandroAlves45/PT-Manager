using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Services;

/// <summary>Calcula os três modos de macros segundo a fórmula de Atwater.</summary>
public static class MacroTargetCalculator
{
    public const decimal MaximumManualKcalDifference = 100m;

    public static MacroCalculationResult CalculateFromPercentage(
        decimal targetKcal,
        PercentageMacroInput input
    )
    {
        ValidateTarget(targetKcal);
        ArgumentNullException.ThrowIfNull(input);

        ValidatePercentage(input.ProteinPercentage, "Protein percentage");
        ValidatePercentage(input.CarbsPercentage, "Carbs percentage");
        ValidatePercentage(input.FatsPercentage, "Fats percentage");

        if (input.ProteinPercentage + input.CarbsPercentage + input.FatsPercentage != 100m)
            throw new DomainException("Macro percentages must total exactly 100.00.");

        var targets = new MacroSummary(
            targetKcal * input.ProteinPercentage / 100m / 4m,
            targetKcal * input.CarbsPercentage / 100m / 4m,
            targetKcal * input.FatsPercentage / 100m / 9m
        );

        return CreateResult(
            MacroDistributionMode.Percentage,
            targets,
            targetKcal,
            input.ProteinPercentage,
            input.CarbsPercentage,
            input.FatsPercentage,
            null,
            null,
            null
        );
    }

    public static MacroCalculationResult CalculateFromGramsPerKg(
        decimal targetKcal,
        decimal weightKg,
        PerKgMacroInput input
    )
    {
        ValidateTarget(targetKcal);
        ArgumentNullException.ThrowIfNull(input);

        if (weightKg <= 0)
            throw new DomainException("Weight must be greater than zero.");
        if (input.ProteinGramsPerKg <= 0 || input.FatsGramsPerKg <= 0)
            throw new DomainException("Protein and fats grams per kg must be greater than zero.");

        var proteinGrams = weightKg * input.ProteinGramsPerKg;
        var fatsGrams = weightKg * input.FatsGramsPerKg;
        var fixedKcal = proteinGrams * 4m + fatsGrams * 9m;

        if (fixedKcal > targetKcal)
            throw new DomainException(
                "Protein and fats allocations exceed the target kcal before carbs are calculated."
            );

        var carbsGrams = (targetKcal - fixedKcal) / 4m;
        var targets = new MacroSummary(proteinGrams, carbsGrams, fatsGrams);

        return CreateResult(
            MacroDistributionMode.GramsPerKg,
            targets,
            targetKcal,
            null,
            null,
            null,
            input.ProteinGramsPerKg,
            input.FatsGramsPerKg,
            weightKg
        );
    }

    public static MacroCalculationResult CalculateFromManualGrams(
        decimal targetKcal,
        ManualMacroInput input
    )
    {
        ValidateTarget(targetKcal);
        ArgumentNullException.ThrowIfNull(input);

        if (input.ProteinGrams < 0 || input.CarbsGrams < 0 || input.FatsGrams < 0)
            throw new DomainException("Manual macro grams cannot be negative.");

        var targets = new MacroSummary(
            input.ProteinGrams,
            input.CarbsGrams,
            input.FatsGrams
        );

        if (targets.Kcal == 0)
            throw new DomainException("Manual macros cannot have a total of zero kcal. Healthy problems may arise.");

        var difference = Math.Abs(targets.Kcal - targetKcal);

        if (difference > MaximumManualKcalDifference)
            throw new DomainException(
                "Manual macros cannot differ from the target by more than 100 kcal."
            );

        return CreateResult(
            MacroDistributionMode.ManualGrams,
            targets,
            targetKcal,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }

    private static MacroCalculationResult CreateResult(
        MacroDistributionMode mode,
        MacroSummary targets,
        decimal targetKcal,
        decimal? proteinPercentage,
        decimal? carbsPercentage,
        decimal? fatsPercentage,
        decimal? proteinGramsPerKg,
        decimal? fatsGramsPerKg,
        decimal? weightUsedForMacros
    ) => new(
        mode,
        targets,
        targetKcal,
        Math.Abs(targets.Kcal - targetKcal),
        proteinPercentage,
        carbsPercentage,
        fatsPercentage,
        proteinGramsPerKg,
        fatsGramsPerKg,
        weightUsedForMacros
    );

    private static void ValidateTarget(decimal targetKcal)
    {
        if (targetKcal <= 0)
            throw new DomainException("Target kcal must be greater than zero.");
    }

    private static void ValidatePercentage(decimal value, string fieldName)
    {
        if (value < 0 || value > 100)
            throw new DomainException($"{fieldName} must be between 0 and 100.");

        // A escala limitada evita aceitar uma soma aparentemente exata criada
        // com precisão superior à permitida no contrato e na base de dados.
        if (decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
            throw new DomainException($"{fieldName} cannot have more than 2 decimal places.");
    }
}
