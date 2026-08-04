using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class NutritionAdjustmentsTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MealPlan_UsesSnapshotAsOnlySourceOfTargets()
    {
        // Arrange
        var macros = MacroTargetCalculator.CalculateFromManualGrams(
            2000,
            new ManualMacroInput(150, 150, 90)
        );
        var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80, macros, Now);

        var plan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Train",
            null,
            new DateOnly(2026, 8, 1),
            null,
            snapshot,
            Now
        );

        // Assert
        Assert.Equal(snapshot.TargetKcal, plan.KcalTarget);
        Assert.Equal(snapshot.CalculatedMacroKcal, plan.Targets.Kcal);
    }

    [Fact]
    public void Food_WhenMacroIsNegative_ThrowsDomainException()
    {
        // Arrange
        var action = () => new Food(
            Guid.NewGuid(),
            "Rice",
            null,
            -10,
            20,
            1,
            null,
            Now
        );

        // Assert
        Assert.Throws<DomainException>(action);
    }
}
