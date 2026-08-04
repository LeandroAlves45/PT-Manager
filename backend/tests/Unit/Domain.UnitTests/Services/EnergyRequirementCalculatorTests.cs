using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Services;

public sealed class EnergyRequirementCalculatorTests
{
    [Fact]
    public void Calculate_HarrisMaleDeficit_ReturnsExpectedUnroundedValues()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.HarrisBenedict,
            weightKg: 80,
            heightCm: 180,
            age: 30,
            sex: BiologicalSex.Male,
            goal: NutritionGoalType.Deficit,
            adjustment: 500
        );

        var result = EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Equal(1853.632m, result.RestingEnergyExpenditureKcal);
        Assert.Equal(2873.12960m, result.TotalDailyEnergyExpenditureKcal);
        Assert.Equal(2373.12960m, result.TargetKcal);
    }

    [Fact]
    public void Calculate_MifflinFemaleMaintenance_ReturnsExpectedRestingEnergy()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.MifflinStJeor,
            weightKg: 60,
            heightCm: 165,
            age: 30,
            sex: BiologicalSex.Female,
            goal: NutritionGoalType.Maintenance,
            adjustment: 0
        );

        // Act
        var result = EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Equal(1320.25m, result.RestingEnergyExpenditureKcal);
    }

    [Fact]
    public void Calculate_Cunningham_DerivesFatFreeMassFromBodyFat()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.Cunningham,
            weightKg: 80,
            heightCm: null,
            age: 30,
            sex: null,
            goal: NutritionGoalType.Maintenance,
            adjustment: 0,
            bodyFatPercentage: 20
        );

        // Act
        var result = EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Equal(1908m, result.RestingEnergyExpenditureKcal);
    }

    [Fact]
    public void Calculate_TinsleyBodyWeight_ReturnsExpectedRestingEnergy()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.Tinsley,
            weightKg: 80,
            heightCm: null,
            age: 30,
            sex: null,
            goal: NutritionGoalType.Maintenance,
            adjustment: 0
        );

        // Act
        var result = EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Equal(1994.0m, result.RestingEnergyExpenditureKcal);
    }

    [Fact]
    public void Calculate_WhenAgeIsBelowEighteen_ThrowsDomainException()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.Tinsley,
            weightKg: 80,
            heightCm: null,
            age: 17,
            sex: null,
            goal: NutritionGoalType.Maintenance,
            adjustment: 0
        );

        // Act
        var action = () => EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Calculate_CunninghamWithoutBodyFat_ThrowsDomainException()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.Cunningham,
            weightKg: 80,
            heightCm: null,
            age: 30,
            sex: null,
            goal: NutritionGoalType.Maintenance,
            adjustment: 0,
            bodyFatPercentage: null
        );

        // Act
        var action = () => EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Calculate_MaintenanceWithAdjustment_ThrowsDomainException()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.HarrisBenedict,
            weightKg: 80,
            heightCm: 180,
            age: 30,
            sex: BiologicalSex.Male,
            goal: NutritionGoalType.Maintenance,
            adjustment: 100
        );

        // Act
        var action = () => EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Calculate_DeficitThatProducesNonPositiveTargetKcal_ThrowsDomainException()
    {
        // Arrange
        var input = CreateInput(
            EnergyFormula.HarrisBenedict,
            weightKg: 40,
            heightCm: 160,
            age: 30,
            sex: BiologicalSex.Female,
            goal: NutritionGoalType.Deficit,
            adjustment: 2000
        );

        // Act
        var action = () => EnergyRequirementCalculator.Calculate(input);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    private static EnergyRequirementInput CreateInput(
        EnergyFormula formula,
        decimal weightKg,
        decimal? heightCm,
        int age,
        BiologicalSex? sex,
        NutritionGoalType goal,
        decimal adjustment,
        decimal? bodyFatPercentage = null
    ) =>
        new(
            formula,
            weightKg,
            heightCm,
            age,
            sex,
            bodyFatPercentage,
            ActivityLevel.ModeratelyActive,
            goal,
            adjustment
        );
}
