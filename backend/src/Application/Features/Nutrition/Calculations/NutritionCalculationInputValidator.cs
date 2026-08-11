using Domain.Exceptions;
using Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Features.Nutrition.Calculations;

/// <summary>Valida a combinação discriminada de energia e macronutrientes.</summary>
public sealed class NutritionCalculationInputValidator : AbstractValidator<NutritionCalculationInput>
{
    private const decimal ProteinKcalPerGram = 4m;
    private const decimal CarbsKcalPerGram = 4m;
    private const decimal FatsKcalPerGram = 9m;

    /// <summary>Configura regras sintáticas, condicionais e semânticas do cálculo.</summary>
    public NutritionCalculationInputValidator()
    {
        RuleFor(input => input.CalculationOrigin)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(origin => origin is "formula" or "manual_energy")
            .WithErrorCode("calculation_origin_invalid")
            .WithMessage("Calculation origin must be either 'formula' or 'manual_energy'.");

        RuleFor(input => input.WeightKg)
            .GreaterThan(0m)
            .WithErrorCode("weight_invalid")
            .WithMessage("Weight must be greater than 0 kg.");

        ConfigureFormulaRules();
        ConfigureManualEnergyRules();

        RuleFor(input => input.MacroMode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(mode => mode is "percentage" or "grams_per_kg" or "manual_grams")
            .WithErrorCode("macro_mode_invalid")
            .WithMessage("Macro mode must be either 'percentage', 'grams_per_kg' or 'manual_grams'.");

        ConfigurePercentageRules();
        ConfigureGramsPerKgRules();
        ConfigureManualGramsRules();

        // A condição impede que o factory receba valores que as regras simples
        // já classificaram como inválidos e evita erros duplicados ou exceções.
        RuleFor(input => input)
            .Custom(ValidateCrossFieldConsistency)
            .When(HasValidShapeForCrossFieldValidation);
    }

    private void ConfigureFormulaRules()
    {
        When(input => input.CalculationOrigin == "formula", () =>
        {
            RuleFor(input => input.EnergyFormula)
                .NotEmpty()
                .Must(IsSupportedEnergyFormula)
                .WithErrorCode("energy_formula_invalid")
                .WithMessage("Energy formula must be one of the supported formulas.");

            RuleFor(input => input.Age)
                .NotNull()
                .GreaterThanOrEqualTo(18)
                .WithErrorCode("nutrition_age_below_minimum")
                .WithMessage("Age must be 18 or older for formula-based calculations.");

            RuleFor(input => input.ActivityLevel)
                .NotEmpty()
                .Must(IsSupportedActivityLevel)
                .WithErrorCode("activity_level_invalid")
                .WithMessage("Activity level must be one of the supported levels.");

            RuleFor(input => input.GoalType)
                .NotEmpty()
                .Must(goal => goal is "maintenance" or "deficit" or "surplus")
                .WithErrorCode("goal_type_invalid")
                .WithMessage("Goal type must be either 'maintenance', 'deficit' or 'surplus'.");

            RuleFor(input => input.GoalAdjustmentKcal)
                .NotNull()
                .GreaterThanOrEqualTo(0m)
                .WithErrorCode("goal_adjustment_kcal_invalid")
                .WithMessage("Goal adjustment kcal must be a non-negative value.");

            RuleFor(input => input.ManualTargetKcal)
                .Null()
                .WithErrorCode("manual_target_kcal_not_allowed")
                .WithMessage("Manual target kcal is not allowed when calculation origin is 'formula'.");

            When(RequiresHeightAndSex, () =>
            {
                RuleFor(input => input.HeightCm)
                    .NotNull()
                    .GreaterThan(0m)
                    .WithErrorCode("height_invalid")
                    .WithMessage("Height must be greater than 0 cm when required by the formula.");

                RuleFor(input => input.Sex)
                    .NotEmpty()
                    .Must(sex => sex is "male" or "female")
                    .WithErrorCode("sex_invalid")
                    .WithMessage("Sex must be either 'male' or 'female'.");
            });

            When(input => input.EnergyFormula == "cunningham", () =>
            {
                // 0 e 100 são inválidos para Cunningham (massa magra ficaria nula ou igual ao peso
                // total) — tem de ser exclusivo, alinhado com HasValidEnergyShape e o Domain.
                RuleFor(input => input.BodyFatPercentage)
                    .NotNull()
                    .ExclusiveBetween(0m, 100m)
                    .WithErrorCode("body_fat_percentage_invalid")
                    .WithMessage("Body fat percentage must be between 0 and 100 when required by the formula.");
            });

            RuleFor(input => input.GoalAdjustmentKcal)
                .Equal(0m)
                .When(input => input.GoalType == "maintenance")
                .WithErrorCode("goal_adjustment_kcal_must_be_zero")
                .WithMessage("Goal adjustment kcal must be 0 when goal type is 'maintenance'.");

            RuleFor(input => input.GoalAdjustmentKcal)
                .GreaterThan(0m)
                .When(input => input.GoalType is "deficit" or "surplus")
                .WithErrorCode("goal_adjustment_kcal_must_be_positive")
                .WithMessage("Goal adjustment kcal must be positive when goal type is 'deficit' or 'surplus'.");
        });
    }

    private void ConfigureManualEnergyRules()
    {
        When(input => input.CalculationOrigin == "manual_energy", () =>
        {
            RuleFor(input => input.ManualTargetKcal)
                .NotNull()
                .GreaterThan(0m)
                .WithErrorCode("manual_target_kcal_invalid")
                .WithMessage(
                    "Manual target kcal must be a positive value when calculation origin is 'manual_energy'.");

            RuleFor(input => input.EnergyFormula)
                .Null()
                .WithErrorCode("energy_formula_not_allowed")
                .WithMessage(
                    "Energy formula is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.HeightCm)
                .Null()
                .WithErrorCode("height_not_allowed")
                .WithMessage(
                    "Height is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.Age)
                .Null()
                .WithErrorCode("age_not_allowed")
                .WithMessage(
                    "Age is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.Sex)
                .Null()
                .WithErrorCode("sex_not_allowed")
                .WithMessage(
                    "Sex is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.BodyFatPercentage)
                .Null()
                .WithErrorCode("body_fat_percentage_not_allowed")
                .WithMessage(
                    "Body fat percentage is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.ActivityLevel)
                .Null()
                .WithErrorCode("activity_level_not_allowed")
                .WithMessage(
                    "Activity level is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.GoalType)
                .Null()
                .WithErrorCode("goal_type_not_allowed")
                .WithMessage(
                    "Goal type is not allowed when calculation origin is 'manual_energy'.");

            RuleFor(input => input.GoalAdjustmentKcal)
                .Null()
                .WithErrorCode("goal_adjustment_kcal_not_allowed")
                .WithMessage(
                    "Goal adjustment kcal is not allowed when calculation origin is 'manual_energy'.");
        });
    }

    private void ConfigurePercentageRules()
    {
        When(input => input.MacroMode == "percentage", () =>
        {
            ConfigurePercentageField(input => input.ProteinPercentage, "protein_percentage");
            ConfigurePercentageField(input => input.CarbsPercentage, "carbs_percentage");
            ConfigurePercentageField(input => input.FatsPercentage, "fats_percentage");

            RuleFor(input => input)
                .Must(HavePercentagesSummingToOneHundred)
                .WithName("Percentages")
                .WithErrorCode("macro_percentage_total_invalid")
                .WithMessage("The sum of protein, carbs and fats percentages must equal 100.");

            RuleFor(input => input.ProteinGramsPerKg)
                .Null()
                .WithErrorCode("protein_grams_per_kg_not_allowed")
                .WithMessage("Protein grams per kg is not allowed when macro mode is 'percentage'.");

            RuleFor(input => input.FatsGramsPerKg)
                .Null()
                .WithErrorCode("fats_grams_per_kg_not_allowed")
                .WithMessage("Fats grams per kg is not allowed when macro mode is 'percentage'.");

            RuleFor(input => input.ProteinGrams)
                .Null()
                .WithErrorCode("protein_grams_not_allowed")
                .WithMessage("Protein grams is not allowed when macro mode is 'percentage'.");

            RuleFor(input => input.CarbsGrams)
                .Null()
                .WithErrorCode("carbs_grams_not_allowed")
                .WithMessage("Carbs grams is not allowed when macro mode is 'percentage'.");

            RuleFor(input => input.FatsGrams)
                .Null()
                .WithErrorCode("fats_grams_not_allowed")
                .WithMessage("Fats grams is not allowed when macro mode is 'percentage'.");
        });
    }

    private void ConfigureGramsPerKgRules()
    {
        When(input => input.MacroMode == "grams_per_kg", () =>
        {
            RuleFor(input => input.ProteinGramsPerKg)
                .NotNull()
                .GreaterThan(0m)
                .WithErrorCode("protein_grams_per_kg_invalid")
                .WithMessage(
                    "Protein grams per kg must be a positive value when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.FatsGramsPerKg)
                .NotNull()
                .GreaterThan(0m)
                .WithErrorCode("fats_grams_per_kg_invalid")
                .WithMessage(
                    "Fats grams per kg must be a positive value when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.ProteinPercentage)
                .Null()
                .WithErrorCode("protein_percentage_not_allowed")
                .WithMessage("Protein percentage is not allowed when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.CarbsPercentage)
                .Null()
                .WithErrorCode("carbs_percentage_not_allowed")
                .WithMessage("Carbs percentage is not allowed when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.FatsPercentage)
                .Null()
                .WithErrorCode("fats_percentage_not_allowed")
                .WithMessage("Fats percentage is not allowed when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.ProteinGrams)
                .Null()
                .WithErrorCode("protein_grams_not_allowed")
                .WithMessage("Protein grams is not allowed when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.CarbsGrams)
                .Null()
                .WithErrorCode("carbs_grams_not_allowed")
                .WithMessage("Carbs grams is not allowed when macro mode is 'grams_per_kg'.");

            RuleFor(input => input.FatsGrams)
                .Null()
                .WithErrorCode("fats_grams_not_allowed")
                .WithMessage("Fats grams is not allowed when macro mode is 'grams_per_kg'.");
        });
    }

    private void ConfigureManualGramsRules()
    {
        When(input => input.MacroMode == "manual_grams", () =>
        {
            RuleFor(input => input.ProteinGrams)
                .NotNull()
                .GreaterThanOrEqualTo(0m)
                .WithErrorCode("protein_grams_invalid")
                .WithMessage(
                    "Protein grams must be a non-negative value when macro mode is 'manual_grams'.");

            RuleFor(input => input.CarbsGrams)
                .NotNull()
                .GreaterThanOrEqualTo(0m)
                .WithErrorCode("carbs_grams_invalid")
                .WithMessage(
                    "Carbs grams must be a non-negative value when macro mode is 'manual_grams'.");

            RuleFor(input => input.FatsGrams)
                .NotNull()
                .GreaterThanOrEqualTo(0m)
                .WithErrorCode("fats_grams_invalid")
                .WithMessage(
                    "Fats grams must be a non-negative value when macro mode is 'manual_grams'.");

            RuleFor(input => input)
                .Must(HavePositiveAtwaterTotal)
                .WithName("AtwaterTotal")
                .WithErrorCode("atwater_total_invalid")
                .WithMessage(
                    "The total energy calculated from protein, carbs and fats grams must be positive.");

            RuleFor(input => input.ProteinPercentage)
                .Null()
                .WithErrorCode("protein_percentage_not_allowed")
                .WithMessage("Protein percentage is not allowed when macro mode is 'manual_grams'.");

            RuleFor(input => input.CarbsPercentage)
                .Null()
                .WithErrorCode("carbs_percentage_not_allowed")
                .WithMessage("Carbs percentage is not allowed when macro mode is 'manual_grams'.");

            RuleFor(input => input.FatsPercentage)
                .Null()
                .WithErrorCode("fats_percentage_not_allowed")
                .WithMessage("Fats percentage is not allowed when macro mode is 'manual_grams'.");

            RuleFor(input => input.ProteinGramsPerKg)
                .Null()
                .WithErrorCode("protein_grams_per_kg_not_allowed")
                .WithMessage("Protein grams per kg is not allowed when macro mode is 'manual_grams'.");

            RuleFor(input => input.FatsGramsPerKg)
                .Null()
                .WithErrorCode("fats_grams_per_kg_not_allowed")
                .WithMessage("Fats grams per kg is not allowed when macro mode is 'manual_grams'.");
        });
    }

    private void ConfigurePercentageField(
        System.Linq.Expressions.Expression<Func<NutritionCalculationInput, decimal?>> expression,
        string errorCodePrefix)
    {
        RuleFor(expression)
            .NotNull()
            .InclusiveBetween(0m, 100m)
            .Must(HaveAtMostTwoDecimalPlaces)
            .WithErrorCode($"{errorCodePrefix}_invalid");
    }

    private static void ValidateCrossFieldConsistency(
        NutritionCalculationInput input,
        ValidationContext<NutritionCalculationInput> context)
    {
        try
        {
            NutritionCalculationFactory.CreateSnapshot(input, DateTime.UnixEpoch);
        }
        catch (DomainException)
        {
            context.AddFailure(new ValidationFailure(
                nameof(NutritionCalculationInput.MacroMode),
                "Macro allocation is not consistent with the calculated target energy.")
            {
                ErrorCode = "macro_allocation_inconsistent"
            });
        }
    }

    private static bool HasValidShapeForCrossFieldValidation(NutritionCalculationInput input) =>
        input.WeightKg > 0m
        && HasValidEnergyShape(input)
        && HasValidMacroShape(input);

    private static bool HasValidEnergyShape(NutritionCalculationInput input)
    {
        if (input.CalculationOrigin == "manual_energy")
            return input.ManualTargetKcal is > 0m;

        if (input.CalculationOrigin != "formula"
            || !IsSupportedEnergyFormula(input.EnergyFormula)
            || input.Age is null or < 18
            || !IsSupportedActivityLevel(input.ActivityLevel)
            || input.GoalType is not ("maintenance" or "deficit" or "surplus")
            || input.GoalAdjustmentKcal is null or < 0m)
            return false;

        if (input.GoalType == "maintenance" && input.GoalAdjustmentKcal != 0m)
            return false;

        if (input.GoalType != "maintenance" && input.GoalAdjustmentKcal <= 0m)
            return false;

        if (RequiresHeightAndSex(input))
            return input.HeightCm is > 0m && input.Sex is "male" or "female";

        return input.EnergyFormula != "cunningham"
            || input.BodyFatPercentage is > 0m and < 100m;
    }

    private static bool HasValidMacroShape(NutritionCalculationInput input) =>
        input.MacroMode switch
        {
            "percentage" =>
                IsValidPercentage(input.ProteinPercentage)
                && IsValidPercentage(input.CarbsPercentage)
                && IsValidPercentage(input.FatsPercentage)
                && HavePercentagesSummingToOneHundred(input),
            "grams_per_kg" => input.ProteinGramsPerKg is > 0m && input.FatsGramsPerKg is > 0m,
            "manual_grams" =>
                input.ProteinGrams is >= 0m
                && input.CarbsGrams is >= 0m
                && input.FatsGrams is >= 0m
                && HavePositiveAtwaterTotal(input),
            _ => false
        };

    private static bool RequiresHeightAndSex(NutritionCalculationInput input) =>
        input.EnergyFormula is "harris_benedict" or "mifflin_st_jeor";

    private static bool IsSupportedEnergyFormula(string? value) =>
        value is "harris_benedict" or "mifflin_st_jeor" or "cunningham" or "tinsley";

    private static bool IsSupportedActivityLevel(string? value)
    {
        if (value is null)
            return false;

        try
        {
            ActivityLevel.FromString(value);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    private static bool IsValidPercentage(decimal? value) =>
        value is >= 0m and <= 100m && HaveAtMostTwoDecimalPlaces(value);

    private static bool HaveAtMostTwoDecimalPlaces(decimal? value) =>
        value is null || value == decimal.Round(value.Value, 2);

    private static bool HavePercentagesSummingToOneHundred(NutritionCalculationInput input) =>
        input.ProteinPercentage is null
        || input.CarbsPercentage is null
        || input.FatsPercentage is null
        || input.ProteinPercentage + input.CarbsPercentage + input.FatsPercentage == 100m;

    private static bool HavePositiveAtwaterTotal(NutritionCalculationInput input) =>
        input.ProteinGrams is null
        || input.CarbsGrams is null
        || input.FatsGrams is null
        || input.ProteinGrams * ProteinKcalPerGram
            + input.CarbsGrams * CarbsKcalPerGram
            + input.FatsGrams * FatsKcalPerGram > 0m;
}


