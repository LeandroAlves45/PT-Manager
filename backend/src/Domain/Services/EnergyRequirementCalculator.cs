using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Services;

/// <summary>
/// Executa fórmulas nutricionais puras. Não lê Client, InitialAssessment ou CheckIn
/// porque o personal trainer deve controlar explicitamente os valores usados.
/// </summary>
public static class EnergyRequirementCalculator
{
    public static EnergyCalculationResult Calculate(EnergyRequirementInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateCommonInput(input);

        var restingEnergy = input.Formula switch
        {
            EnergyFormula.HarrisBenedict => CalculateHarrisBenedict(input),
            EnergyFormula.MifflinStJeor => CalculateMifflinStJeor(input),
            EnergyFormula.Cunningham => CalculateCunningham(input),
            EnergyFormula.Tinsley => CalculateTinsley(input),
            _ => throw new DomainException("Unsupported energy formula.")
        };

        var totalDailyEnergy = restingEnergy * input.ActivityLevel.Factor;
        var target = input.GoalType switch
        {
            NutritionGoalType.Maintenance => totalDailyEnergy,
            NutritionGoalType.Deficit => totalDailyEnergy - input.GoalAdjustmentKcal,
            NutritionGoalType.Surplus => totalDailyEnergy + input.GoalAdjustmentKcal,
            _ => throw new DomainException("Unsupported nutrition goal")
        };

        if (target <= 0)
            throw new DomainException("Target kcal must be greater than zero.");

        return new EnergyCalculationResult(input, restingEnergy, totalDailyEnergy, target);
    }

    private static decimal CalculateHarrisBenedict(EnergyRequirementInput input)
    {
        var height = RequireHeight(input);
        var sex = RequireSex(input);
        return sex == BiologicalSex.Male
            ? 88.362m + 13.397m * input.WeightKg + 4.799m * height - 5.677m * input.AgeUsed
            : 447.593m + 9.247m * input.WeightKg + 3.098m * height - 4.330m * input.AgeUsed;
    }

    private static decimal CalculateMifflinStJeor(EnergyRequirementInput input)
    {
        var height = RequireHeight(input);
        var sex = RequireSex(input);
        var common = 10m * input.WeightKg + 6.25m * height - 5m * input.AgeUsed;

        return sex == BiologicalSex.Male ? common + 5m : common - 161m;
    }

    private static decimal CalculateCunningham(EnergyRequirementInput input)
    {
        if (!input.BodyFatPercentage.HasValue ||
            input.BodyFatPercentage.Value <= 0 ||
            input.BodyFatPercentage.Value >= 100)
            throw new DomainException(
                "Cunningham requires body fat percentage greater than zero and lower than 100.");

        var fatFreeMassKg = input.WeightKg * (1m - input.BodyFatPercentage.Value / 100m);
        return 500m + 22m * fatFreeMassKg;
    }

    private static decimal CalculateTinsley(EnergyRequirementInput input) =>
        24.8m * input.WeightKg + 10m;

    private static void ValidateCommonInput(EnergyRequirementInput input)
    {
        if (input.WeightKg <= 0)
            throw new DomainException("Weight must be greater than zero.");
        if (input.AgeUsed < 18)
            throw new DomainException("Energy formulas are available only from 18 years of age.");
        if (input.ActivityLevel is null)
            throw new DomainException("Activity level is required.");
        if (input.GoalAdjustmentKcal < 0)
            throw new DomainException("Goal adjustment must be a non-negative magnitude.");
        if (input.GoalType == NutritionGoalType.Maintenance && input.GoalAdjustmentKcal != 0)
            throw new DomainException("Maintenance requires a zero kcal adjustment.");
        if (input.GoalType != NutritionGoalType.Maintenance && input.GoalAdjustmentKcal == 0)
            throw new DomainException("Deficit or surplus requires a positive kcal adjustment.");
    }

    private static decimal RequireHeight(EnergyRequirementInput input)
    {
        if (input.HeightCm is not > 0)
            throw new DomainException("The selected formula requires a height greater than zero.");

        return input.HeightCm.Value;
    }

    private static BiologicalSex RequireSex(EnergyRequirementInput input) =>
        input.SexUsed ?? throw new DomainException("The selected formula requires biological sex");
}
