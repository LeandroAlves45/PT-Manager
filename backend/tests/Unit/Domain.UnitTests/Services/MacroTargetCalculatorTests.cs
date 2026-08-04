using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Services;

public sealed class MacroTargetCalculatorTests
{
    public static TheoryData<decimal> InvalidPercentageTotals => new()
    {
        99.99m,
        100.01m,
    };

    [Fact]
    public void CalculateFromPercentages_WhenTotalIsOneHundred_ReturnsExpectedTargets()
    {
        // Arrange
        var result = MacroTargetCalculator.CalculateFromPercentage(
            2000,
            new PercentageMacroInput(30, 40, 30)
        );

        Assert.Equal(150m, result.Targets.ProteinGrams);
        Assert.Equal(200m, result.Targets.CarbsGrams);
        Assert.Equal(2000m / 30m, result.Targets.FatsGrams);
    }

    [Theory]
    [MemberData(nameof(InvalidPercentageTotals))]
    public void CalculateFromPercentages_WhenTotalIsNotExact_ThrowsDomainException(
        decimal total
    )
    {
        // Arrange
        var input = new PercentageMacroInput(total - 70m, 40, 30);

        // Act
        var action = () => MacroTargetCalculator.CalculateFromPercentage(2000, input);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CalculateFromGramsPerKg_FillsRemainingEnergyWithCarbs()
    {
        // Arrange
        var result = MacroTargetCalculator.CalculateFromGramsPerKg(
            2000,
            80,
            new PerKgMacroInput(2, 1)
        );

        Assert.Equal(160m, result.Targets.ProteinGrams);
        Assert.Equal(160m, result.Targets.CarbsGrams);
        Assert.Equal(80m, result.Targets.FatsGrams);
        Assert.Equal(0m, result.KcalDifference);
    }

    [Fact]
    public void CalculateFromGramsPerKg_WhenFixedMacrosExceedTarget_ThrowsDomainException()
    {
        // Arrange
        var action = () => MacroTargetCalculator.CalculateFromGramsPerKg(
            1000,
            80,
            new PerKgMacroInput(3, 2)
        );

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CalculateFromManualGrams_WithExactlyOneHundredKcalDifference_IsAccepted()
    {
        // Arrange
        var result = MacroTargetCalculator.CalculateFromManualGrams(
            2000,
            new ManualMacroInput(150, 150, 100)
        );

        Assert.Equal(100m, result.KcalDifference);
    }

    [Fact]
    public void CalculateFromManualGrams_AboveOneHundredKcalDifference_ThrowsDomainException()
    {
        // Arrange
        var action = () => MacroTargetCalculator.CalculateFromManualGrams(
            2000,
            new ManualMacroInput(150.0025m, 150, 100)
        );

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CalculateFromManualGrams_WhenAllMacrosAreZero_ThrowsDomainException()
    {
        // Arrange
        var action = () => MacroTargetCalculator.CalculateFromManualGrams(
            50,
            new ManualMacroInput(0, 0, 0)
        );

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }
}
