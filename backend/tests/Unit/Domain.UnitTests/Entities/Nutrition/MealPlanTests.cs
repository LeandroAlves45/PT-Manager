using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class MealPlanTests
{
    [Fact]
    public void Constructor_StartAfterEnd_ThrowsDomainException()
    {
        // Arrange
        var startsDate = new DateOnly(2026, 8, 10);
        var endsDate = new DateOnly(2026, 8, 1);
        var targets = new MacroSummary(100, 200, 50);
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Meal Plan",
            null,
            startsDate,
            endsDate,
            targets,
            now
        ));
        Assert.Equal("Meal Plan ends date cannot be before starts date", exception.Message);
    }

    [Fact]
    public void AddMeal_DuplicateOrderNumber_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 1);
        var endsDate = new DateOnly(2026, 8, 10);
        var targets = new MacroSummary(100, 200, 50);
        var mealPlan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Meal Plan",
            null,
            startsDate,
            endsDate,
            targets,
            now
        );

        // Act
        mealPlan.AddMeal("Lunch", 1, now);

        // Assert
        Assert.Throws<DomainException>(() => mealPlan.AddMeal("Dinner", 1, now));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddMeal_BlankMealType_ThrowsDomainException(string invalidMealType)
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 1);
        var endsDate = new DateOnly(2026, 8, 10);
        var targets = new MacroSummary(100, 200, 50);
        var mealPlan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Meal Plan",
            null,
            startsDate,
            endsDate,
            targets,
            now
        );

        // Act & Assert
        Assert.Throws<DomainException>(() => mealPlan.AddMeal(invalidMealType, 1, now));
    }

    [Fact]
    public void AddMeal_MealTypeTooLong_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 1);
        var endsDate = new DateOnly(2026, 8, 10);
        var targets = new MacroSummary(100, 200, 50);
        var mealPlan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Meal Plan",
            null,
            startsDate,
            endsDate,
            targets,
            now
        );

        var tooLong = new string('a', 51);

        // Act & Assert
        Assert.Throws<DomainException>(() => mealPlan.AddMeal(tooLong, 1, now));
    }

    [Fact]
    public void AddSuplement_DuplicateSupplementId_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 1);
        var endsDate = new DateOnly(2026, 8, 10);
        var targets = new MacroSummary(100, 200, 50);
        var mealPlan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Meal Plan",
            null,
            startsDate,
            endsDate,
            targets,
            now
        );
        var mealPlanMeal = mealPlan.AddMeal("Lunch", 1, now);
        var supplementId1 = Guid.NewGuid();
        mealPlanMeal.AddSupplement(
            supplementId1,
            null,
            10,
            1,
            now
        );
        // Act & Assert
        Assert.Throws<DomainException>(() => mealPlanMeal.AddSupplement(
            supplementId1,
            null,
            10,
            1,
            now
        ));
    }

    [Fact]
    public void Reactivate_DeletedMealPlan_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var mealPlan = new MealPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Meal Plan",
            null,
            new DateOnly(2026, 8, 1),
            null,
            new MacroSummary(100, 200, 50),
            now
        );
        mealPlan.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() => mealPlan.Reactivate(now.AddMinutes(1)));
    }
}

