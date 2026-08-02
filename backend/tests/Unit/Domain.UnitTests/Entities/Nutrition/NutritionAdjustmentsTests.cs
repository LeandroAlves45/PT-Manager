using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class NutritionAdjustmentsTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MealPlan_AllowsKcalTargetDifferentFromDerivedMacros()
    {
        // Arrange
        var macros = new MacroSummary(180, 220, 70);

        // Act
        var plan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Treino",
            null,
            new DateOnly(2026, 8, 1),
            null,
            2500,
            macros,
            Now
        );

        // Assert
        Assert.NotEqual(plan.KcalTarget, plan.Targets.Kcal);
    }

    [Fact]
    public void MealPlan_WhenKcalTargetIsNegative_ThrowsDomainException()
    {
        // Arrange
        var action = () => new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Treino",
            null,
            new DateOnly(2026, 8, 1),
            null,
            -100,
            new MacroSummary(0, 0, 0),
            Now
        );

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Food_WhenMacroIsNegative_ThrowsDomainException()
    {
        // Arrange
        var action = () => new Food(
            Guid.NewGuid(),
            "Arroz",
            null,
            -10,
            20,
            1,
            null,
            Now
        );

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }
}
