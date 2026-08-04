using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class FoodTests
{
    [Fact]
    public void Constructor_NameWithOuterWhitespace_StoresNormalizedName()
    {
        // Arrange & Act
        var food = new Food(
            null,
            "  Chicken breast  ",
            null,
            31,
            0,
            3.6m,
            null,
            new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc)
        );

        // Assert
        Assert.Equal("Chicken breast", food.Name);
    }

    [Fact]
    public void Update_NameWithOuterWhitespace_StoresNormalizedName()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var food = new Food(null, "Rice", null, 2.7m, 28, 0.3m, null, now);

        // Act
        food.Update("  Brown rice  ", null, 2.7m, 25.6m, 1m, null, now);

        // Assert
        Assert.Equal("Brown rice", food.Name);
    }

    [Fact]
    public void Update_DeletedFood_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var food = new Food(null, "Rice", null, 2.7m, 28, 0.3m, null, now);
        food.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            food.Update("Brown rice", null, 2.7m, 25.6m, 1m, null, now));
    }

    [Fact]
    public void SoftDelete_ActiveFood_DeactivatesAndDeletesFood()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var food = new Food(null, "Rice", null, 2.7m, 28, 0.3m, null, now);

        // Act
        food.SoftDelete(now.AddMinutes(1));

        // Assert
        Assert.Equal((false, true), (food.IsActive, food.IsDeleted));
    }
}
