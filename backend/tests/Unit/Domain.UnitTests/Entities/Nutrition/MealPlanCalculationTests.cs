using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class MealPlanCalculationTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_DerivesRelationalTargetsFromSnapshot()
    {
        // Arrange
        var snapshot = CreateFormulaSnapshot(weightKg: 80);

        // Act
        var plan = CreatePlan(snapshot);

        // Assert
        Assert.Equal(snapshot.TargetKcal, plan.KcalTarget);
        Assert.Equal(snapshot.ProteinTargetGrams, plan.Targets.ProteinGrams);
        Assert.Equal(snapshot.CarbsTargetGrams, plan.Targets.CarbsGrams);
        Assert.Equal(snapshot.FatsTargetGrams, plan.Targets.FatsGrams);
    }

    [Fact]
    public void ReplaceCalculation_ReplacesSnapshotAndAllRelationalTargetsAtomically()
    {
        // Arrange
        var plan = CreatePlan(CreateFormulaSnapshot(weightKg: 80));
        var replacement = CreateManualSnapshot(weightKg: 78, targetKcal: 2100);

        // Act
        plan.ReplaceCalculation(replacement, Now.AddDays(1));

        // Assert
        Assert.Same(replacement, plan.CalculationSnapshot);
        Assert.Equal(replacement.TargetKcal, plan.KcalTarget);
        Assert.Equal(replacement.ProteinTargetGrams, plan.Targets.ProteinGrams);
    }

    [Fact]
    public void Snapshot_RoundsOnlyFinalValuesAwayFromZero()
    {
        // Arrange
        var snapshot = CreateFormulaSnapshot(weightKg: 80);

        // Assert
        Assert.Equal(2, GetDecimalPlaces(snapshot.TargetKcal));
        Assert.Equal(2, GetDecimalPlaces(snapshot.FatsTargetGrams));
        Assert.Equal(80, snapshot.WeightKgUsed);
    }

    [Fact]
    public void ExistingSnapshot_DoesNotDependOnLaterClientOrCheckInValues()
    {
        // Arrange
        var snapshot = CreateFormulaSnapshot(weightKg: 80);
        var plan = CreatePlan(snapshot);

        // Act
        var unrelatedLaterWeight = 78m;

        // Assert
        Assert.NotEqual(unrelatedLaterWeight, plan.CalculationSnapshot.WeightKgUsed);
        Assert.Equal(80m, plan.CalculationSnapshot.WeightKgUsed);
    }

    [Fact]
    public void FromManualEnergy_WhenGramsPerKgUsedDifferentWeight_ThrowsDomainException()
    {
        // Arrange
        var macros = MacroTargetCalculator.CalculateFromGramsPerKg(
            2000,
            80,
            new PerKgMacroInput(2, 1)
        );

        var action = () => NutritionCalculationSnapshot.FromManualEnergy(
            78,
            macros,
            Now
        );

        Assert.Throws<DomainException>(action);
    }

    private static MealPlan CreatePlan(NutritionCalculationSnapshot snapshot) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Fat loss",
        null,
        new DateOnly(2026, 8, 2),
        null,
        snapshot,
        Now
    );

    private static NutritionCalculationSnapshot CreateFormulaSnapshot(decimal weightKg)
    {
        var energy = EnergyRequirementCalculator.Calculate(new EnergyRequirementInput(
            EnergyFormula.HarrisBenedict,
            weightKg,
            180,
            30,
            BiologicalSex.Male,
            null,
            ActivityLevel.ModeratelyActive,
            NutritionGoalType.Deficit,
            500
        ));

        var macros = MacroTargetCalculator.CalculateFromPercentage(
            energy.TargetKcal,
            new PercentageMacroInput(30, 40, 30)
        );

        return NutritionCalculationSnapshot.FromFormula(energy, macros, Now);
    }

    private static NutritionCalculationSnapshot CreateManualSnapshot(decimal weightKg, decimal targetKcal)
    {
        var macros = MacroTargetCalculator.CalculateFromGramsPerKg(
            targetKcal,
            weightKg,
            new PerKgMacroInput(2, 1)
        );

        return NutritionCalculationSnapshot.FromManualEnergy(weightKg, macros, Now);
    }

    private static int GetDecimalPlaces(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
