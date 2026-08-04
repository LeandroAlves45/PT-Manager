using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public sealed class NutritionProfileValueObjectsTests
{
    public static TheoryData<string, string, decimal> ActivityPresets => new()
    {
        { "sedentary", "sedentary", 1.200m },
        { "lightly_active", "lightly_active", 1.375m },
        { "moderately_active", "moderately_active", 1.550m },
        { "very_active", "very_active", 1.725m },
        { "extremely_active", "extremely_active", 1.900m },
    };

    [Theory]
    [InlineData("male")]
    [InlineData("MALE")]
    public void BiologicalSex_FromMaleVariant_ReturnsMale(string value)
    {
        // Act
        var result = BiologicalSex.FromString(value);

        // Assert
        Assert.Equal(BiologicalSex.Male, result);
    }

    [Fact]
    public void BiologicalSex_FromAnother_ThrowsDomainException()
    {
        // Arrange
        var action = () => BiologicalSex.FromString("other");

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void BirthDate_CalculateAge_BeforeBirthdaySubtractsOneYear()
    {
        // Arrange
        var birthDate = BirthDate.Create(
            new DateOnly(2000, 8, 3),
            new DateOnly(2026, 8, 2)
        );

        // Assert
        Assert.Equal(25, birthDate.CalculateAge(new DateOnly(2026, 8, 2)));
    }

    [Fact]
    public void BirthDate_CalculateAge_OnBirthdayReturnsCompletedYears()
    {
        // Arrange
        var birthDate = BirthDate.Create(
            new DateOnly(2000, 8, 3),
            new DateOnly(2026, 8, 3)
        );

        // Assert
        Assert.Equal(26, birthDate.CalculateAge(new DateOnly(2026, 8, 3)));
    }

    [Fact]
    public void BirthDate_CalculateAge_ForLeapDayUsesDateOnlyCalendarRule()
    {
        // Arrange
        var birthDate = BirthDate.Create(
            new DateOnly(2000, 2, 29),
            new DateOnly(2025, 2, 28)
        );

        // Assert
        Assert.Equal(25, birthDate.CalculateAge(new DateOnly(2025, 2, 28)));
    }

    [Fact]
    public void BirthDate_CreateWithFutureDate_ThrowsDomainException()
    {
        // Arrange
        var action = () => BirthDate.Create(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 2)
        );

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [MemberData(nameof(ActivityPresets))]
    public void ActivityLevel_FromKnownKey_ReturnsExpectedPreset(
        string input,
        string expectedValue,
        decimal expectedFactor
    )
    {
        // Act
        var result = ActivityLevel.FromString(input);

        // Assert
        Assert.Equal(expectedValue, result.Value);
        Assert.Equal(expectedFactor, result.Factor);
    }
}
