using Domain.Entities.Nutrition;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class FoodTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidValues_CreatesNormalizedActiveGlobalFood()
    {
        var food = new Food(null, "  Chicken breast  ", "  Fresh  ", 31m, 0m, 3.6m, 0m, Now);

        Assert.Equal(("Chicken breast", "Fresh", true, null),
            (food.Name, food.Description, food.IsActive, food.OwnerTrainerId));
    }

    [Fact]
    public void Constructor_WhenOwnerTrainerIdIsEmpty_ThrowsDomainException()
    {
        var action = () => new Food(Guid.Empty, "Chicken", null, 31m, 0m, 3.6m, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(101, 0, 0)]
    [InlineData(0, 101, 0)]
    [InlineData(0, 0, 101)]
    [InlineData(60, 60, 0)]
    public void Constructor_WhenMacrosAreInvalid_ThrowsDomainException(int protein, int carbs, int fats)
    {
        var action = () => new Food(null, "Invalid", null, protein, carbs, fats, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetActive_ToSameValue_DoesNotChangeTimestamp()
    {
        var food = CreateFood();

        food.SetActive(true, Now.AddMinutes(1));

        Assert.Equal(Now, food.UpdatedAt);
    }

    [Fact]
    public void SetActive_ToFalse_ArchivesFood()
    {
        var food = CreateFood();

        food.SetActive(false, Now.AddMinutes(1));

        Assert.False(food.IsActive);
    }

    [Fact]
    public void Update_AfterArchive_UpdatesEditableValues()
    {
        var food = CreateFood();
        food.SetActive(false, Now.AddMinutes(1));

        food.Update("  Chicken thigh  ", null, 26m, 0m, 9m, null, Now.AddMinutes(2));

        Assert.Equal("Chicken thigh", food.Name);
    }

    private static Food CreateFood() =>
        new(null, "Chicken breast", null, 31m, 0m, 3.6m, null, Now);
}
