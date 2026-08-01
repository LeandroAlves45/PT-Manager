using Domain.Entities.Assessments;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Assessments;

public sealed class CheckInTests
{
    private static readonly DateTime TestNow = new(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

    private static CheckIn CreateValid(
        BodyMeasurements? bodyMeasurements = null,
        CheckInFeedback? feedback = null,
        int? trainingAdherenceScore = null,
        int? nutritionAdherenceScore = null) =>
        new(
            ownerTrainerId: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            checkInDate: new DateOnly(2026, 7, 25),
            targetDate: null,
            weightKg: 80,
            bodyFatPercentage: null,
            notes: null,
            bodyMeasurements: bodyMeasurements,
            feedback: feedback,
            trainingAdherenceScore: trainingAdherenceScore,
            nutritionAdherenceScore: nutritionAdherenceScore,
            now: TestNow
        );

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Constructor_TrainingAdherenceScoreAtBoundaries_Accepted(int score)
    {
        // Act
        var checkin = CreateValid(trainingAdherenceScore: score);

        // Assert
        Assert.Equal(score, checkin.TrainingAdherenceScore);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_TrainingAdherenceScoreOutOfRange_ThrowsException(int score)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(trainingAdherenceScore: score));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_NutritionAdherenceScoreOutOfRange_ThrowsException(int score)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(nutritionAdherenceScore: score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Constructor_NutritionAdherenceScoreAtBoundaries_Accepted(int score)
    {
        // Act
        var checkin = CreateValid(nutritionAdherenceScore: score);

        // Assert
        Assert.Equal(score, checkin.NutritionAdherenceScore);
    }

    [Fact]
    public void Constructor_MeasurementsNull_DefaultsToEmpty()
    {
        // Act
        var checkin = CreateValid(bodyMeasurements: null);

        // Assert
        Assert.Equal(BodyMeasurements.Empty, checkin.BodyMeasurements);
    }

    [Fact]
    public void Constructor_FeedbackNull_DefaultsToEmpty()
    {
        // Act
        var checkin = CreateValid(feedback: null);

        // Assert
        Assert.Equal(CheckInFeedback.Empty, checkin.Feedback);
    }

    [Fact]
    public void Correct_UpdatesAdherenceScoresAndRevalidates()
    {
        // Arrange
        var checkin = CreateValid(
            trainingAdherenceScore: 50,
            nutritionAdherenceScore: 60
        );

        // Act
        checkin.Correct(
            targetDate: null,
            weightKg: null,
            bodyFatPercentage: null,
            notes: null,
            bodyMeasurements: null,
            feedback: null,
            trainingAdherenceScore: 80,
            nutritionAdherenceScore: 90,
            now: TestNow.AddMinutes(1)
        );

        // Assert
        Assert.Equal(
            (80, 90),
            (checkin.TrainingAdherenceScore, checkin.NutritionAdherenceScore)
        );
    }

    [Fact]
    public void Correct_InvalidAdherenceScores_ThrowsDomainException()
    {
        // Arrange
        var checkin = CreateValid();

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            checkin.Correct(
                targetDate: null,
                weightKg: null,
                bodyFatPercentage: null,
                notes: null,
                bodyMeasurements: null,
                feedback: null,
                trainingAdherenceScore: 101,
                nutritionAdherenceScore: null,
                now: TestNow.AddMinutes(1)
            )
        );
    }

    [Fact]
    public void Correct_AfterSoftDelete_ThrowsDomainException()
    {
        // Arrange
        var checkin = CreateValid();
        checkin.SoftDelete(TestNow);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            checkin.Correct(
                targetDate: null,
                weightKg: null,
                bodyFatPercentage: null,
                notes: null,
                bodyMeasurements: null,
                feedback: null,
                trainingAdherenceScore: 80,
                nutritionAdherenceScore: 90,
                now: TestNow.AddMinutes(1)
            )
        );
    }


    [Fact]
    public void Correct_DeletedCheckIn_ThrowsDomainException()
    {
        // Arrange
        var checkin = CreateValid();
        checkin.SoftDelete(TestNow);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            checkin.Correct(
                targetDate: null,
                weightKg: 79,
                bodyFatPercentage: null,
                notes: null,
                bodyMeasurements: null,
                feedback: null,
                trainingAdherenceScore: null,
                nutritionAdherenceScore: null,
                now: TestNow.AddMinutes(1)
            )
        );
    }
}
