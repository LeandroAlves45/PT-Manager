using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class MealPlanTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void Constructor_WhenNameIsNull_ThrowsDomainException()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

        var action = () => new MealPlan(
            Guid.NewGuid(), Guid.NewGuid(), null!, null,
            new DateOnly(2026, 8, 2), null, CreateSnapshot(), now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_StartAfterEnd_ThrowsDomainException()
    {
        // Arrange
        var action = () => new MealPlan(
            Guid.NewGuid(), Guid.NewGuid(), "Test Meal Plan", null,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 1),
            CreateSnapshot(), Now);

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void AddMeal_DuplicateOrderNumber_ThrowsDomainException()
    {
        // Arrange
        var plan = CreatePlan();
        plan.AddMeal("Lunch", 1, Now);

        // Act
        var action = () => plan.AddMeal("Dinner", 1, Now);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddMeal_BlankMealType_ThrowsDomainException(string mealType)
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var action = () => plan.AddMeal(mealType, 1, Now);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void AddMeal_MealTypeAboveMaximumLength_ThrowsDomainException()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var action = () => plan.AddMeal(new string('a', 51), 1, Now);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void AddSupplement_DuplicateSupplementId_ThrowsDomainException()
    {
        // Arrange
        var meal = CreatePlan().AddMeal("Lunch", 1, Now);
        var supplementId = Guid.NewGuid();
        meal.AddSupplement(supplementId, null, 10, 1, Now);

        // Act
        var action = () => meal.AddSupplement(supplementId, null, 10, 1, Now);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reactivate_DeletedMealPlan_ThrowsDomainException()
    {
        // Arrange
        var plan = CreatePlan();
        plan.SoftDelete(Now);

        // Act
        Action action = () => plan.Reactivate(Now.AddMinutes(1));

        // Assert
        Assert.Throws<DomainException>(action);
    }

    private static MealPlan CreatePlan() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Test Meal Plan",
        null,
        new DateOnly(2026, 8, 2),
        null,
        CreateSnapshot(),
        Now
    );

    private static NutritionCalculationSnapshot CreateSnapshot()
    {
        var macros = MacroTargetCalculator.CalculateFromPercentage(
            2000,
            new PercentageMacroInput(30, 40, 30)
        );

        return NutritionCalculationSnapshot.FromManualEnergy(80, macros, Now);
    }
}
