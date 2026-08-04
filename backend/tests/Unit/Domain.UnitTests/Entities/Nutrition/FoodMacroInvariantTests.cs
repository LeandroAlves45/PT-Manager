using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class FoodMacroInvariantTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    public static TheoryData<decimal> InvalidIndividualMacros => new()
    {
        -0.01m,
        100.01m,
    };

    [Fact]
    public void Constructor_WhenMacrosTotalIsExactly100_AcceptsFood()
    {
        // Arrange
        var food = new Food(
            null,
            "Test Food",
            null,
            30,
            50,
            20,
            null,
            Now
        );

        // Assert
        Assert.Equal(100m, food.Protein + food.Carbs + food.Fats);
    }

    [Fact]
    public void Constructor_WhenMacroTotalExceeds100_ThrowsDomainException()
    {
        // Arrange
        var action = () => new Food(
            null,
            "Test Food",
            null,
            40,
            50,
            20.01m,
            null,
            Now
        );

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [MemberData(nameof(InvalidIndividualMacros))]
    public void Constructor_WhenIndividualMacroIsOutsideRange_ThrowsDomainException(
        decimal protein
    )
    {
        // Arrange
        var action = () => new Food(
            null,
            "Invalid Food",
            null,
            protein,
            0,
            0,
            null,
            Now
        );

        // Assert
        Assert.Throws<DomainException>(action);
    }
}
