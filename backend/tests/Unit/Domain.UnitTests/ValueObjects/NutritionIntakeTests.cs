using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public sealed class NutritionIntakeTests
{
    private static NutritionIntake CreateValid(
        int? sleepQuality = null,
        int? mood = null,
        decimal? avgWaterLitersPerDay = null,
        string? foodPreferences = null) =>
        new(
            foodPreferences: foodPreferences,
            dislikedFoods: null,
            foodIntolerances: null,
            foodAllergies: null,
            dietaryRestrictions: null,
            dailyRoutine: null,
            sleepQuality: sleepQuality,
            mood: mood,
            stressLevel: null,
            avgWaterLitersPerDay: avgWaterLitersPerDay,
            hungriestTimeOfDay: null,
            usesSupplements: null,
            currentSupplements: null,
            otherNotes: null
        );

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_SleepQualityInRange_Accepted(int sleepQuality)
    {
        // Act
        var intake = CreateValid(sleepQuality: sleepQuality);

        // Assert
        Assert.Equal(sleepQuality, intake.SleepQuality);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_SleepQualityOutOfRange_ThrowsException(int sleepQuality)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(sleepQuality: sleepQuality));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_MoodOutOfRange_ThrowsException(int mood)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(mood: mood));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_MoodInRange_Accepted(int mood)
    {
        // Act
        var intake = CreateValid(mood: mood);

        // Assert
        Assert.Equal(mood, intake.Mood);
    }

    [Fact]
    public void Constructor_ZeroWater_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(avgWaterLitersPerDay: 0));
    }

    [Fact]
    public void Constructor_NegativeWater_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(avgWaterLitersPerDay: -1));
    }

    [Fact]
    public void Constructor_PositiveWater_Accepted()
    {
        // Act
        var intake = CreateValid(avgWaterLitersPerDay: 2.5m);

        // Assert
        Assert.Equal(2.5m, intake.AvgWaterLitersPerDay);
    }

    [Fact]
    public void Constructor_TextWithinLimit_Accepted()
    {
        // Act
        var text = new string('a', 2000);
        var intake = CreateValid(foodPreferences: text);

        // Assert
        Assert.Equal(text, intake.FoodPreferences);
    }

    [Fact]
    public void Constructor_TextExceedsLimit_ThrowsException()
    {
        // Act & Assert
        var text = new string('a', 2001);
        Assert.Throws<DomainException>(() => CreateValid(foodPreferences: text));
    }

    [Fact]
    public void Constructor_WhiteSpaceText_NormalizesToNull()
    {
        // Act
        var intake = CreateValid(foodPreferences: "   ");

        // Assert
        Assert.Null(intake.FoodPreferences);
    }
}
