using Domain.Entities.Assessments;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Assessments;

public sealed class InitialAssessmentTests
{
    private static readonly DateTime TestNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    private static InitialAssessment CreateValid(ActivityLevel? activityLevel = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            80,
            180,
            20,
            null,
            "intermediate",
            activityLevel ?? ActivityLevel.ModeratelyActive,
            "lose weight",
            null,
            null,
            null,
            TestNow
        );

    private static NutritionIntake CreateNutritionIntake(
        int? sleepQuality = null) =>
        new(
            null, null, null, null, null, null,
            sleepQuality: sleepQuality,
            null, null, null, null, null, null, null
        );

    [Fact]
    public void Constructor_ProfessionAtMaxLength_Accepted()
    {
        // Arrange
        var profession = new string('a', 255);

        // Act
        var assessment = new InitialAssessment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            80,
            180,
            20,
            null,
            "intermediate",
            ActivityLevel.ModeratelyActive,
            "lose weight",
            profession,
            null,
            null,
            TestNow
        );

        // Assert
        Assert.Equal(profession, assessment.Profession);
    }

    [Fact]
    public void Constructor_ProfessionExceedsLimit_ThrowsException()
    {
        // Arrange
        var profession = new string('a', 256);

        // Act & Assert
        var action = () => new InitialAssessment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            80,
            180,
            20,
            null,
            "intermediate",
            ActivityLevel.ModeratelyActive,
            "lose weight",
            profession,
            null,
            null,
            TestNow
        );

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_ReplacesNewAssesmentFields()
    {
        // Arrange
        var assessment = CreateValid();
        var measurements = new BodyMeasurements(80, null, null, null, null, null, null, null, null);
        var nutritionIntake = CreateNutritionIntake(sleepQuality: 4);

        // Act
        assessment.Update(
            weightKg: 80,
            heightCm: 180,
            bodyFatPercentage: null,
            medicalConditions: null,
            fitnessLevel: "intermediate",
            activityLevel: ActivityLevel.ModeratelyActive,
            goals: "lose weight",
            profession: "Software Engineer",
            bodyMeasurements: measurements,
            nutritionIntake: nutritionIntake,
            now: TestNow.AddMinutes(1)
        );

        // Assert
        Assert.Equal(
            ("Software Engineer", measurements, nutritionIntake),
            (assessment.Profession, assessment.BodyMeasurements, assessment.NutritionIntake)
        );
    }

    [Fact]
    public void Constructor_WithoutComplexValues_UsesEmptyValueObjects()
    {
        // Act
        var assessment = CreateValid();

        // Assert
        Assert.Equal(BodyMeasurements.Empty, assessment.BodyMeasurements);
        Assert.Equal(NutritionIntake.Empty, assessment.NutritionIntake);
    }

    [Fact]
    public void Update_AfterSoftDelete_ThrowsException()
    {
        // Arrange
        var assessment = CreateValid();
        assessment.SoftDelete(TestNow);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            assessment.Update(
                weightKg: 80,
                heightCm: 180,
                bodyFatPercentage: null,
                medicalConditions: null,
                fitnessLevel: "intermediate",
                activityLevel: ActivityLevel.ModeratelyActive,
                goals: "lose weight",
                profession: null,
                bodyMeasurements: null,
                nutritionIntake: null,
                now: TestNow.AddMinutes(1)
            )
        );
    }

    [Fact]
    public void Constructor_WithActivityLevel_StoresSuggestion()
    {
        // Arrange
        var assessment = CreateValid();

        // Assert
        Assert.Equal(ActivityLevel.ModeratelyActive, assessment.ActivityLevel);
    }

    [Fact]
    public void Constructor_WithoutActivityLevel_ThrowsException()
    {
        // Act & Assert
        var action = () => new InitialAssessment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            80,
            180,
            20,
            null,
            "intermediate",
            null!,
            "lose weight",
            null,
            null,
            null,
            TestNow
        );

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Constructor_WithClosedBodyFatBoundary_ThrowsDomainException(int bodyFatPercentage)
    {
        // Act & Assert
        var action = () => new InitialAssessment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            80,
            180,
            (decimal)bodyFatPercentage,
            null,
            "intermediate",
            ActivityLevel.ModeratelyActive,
            "lose weight",
            null,
            null,
            null,
            TestNow
        );
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Update_WithDifferentActivityLevel_ReplacesSuggestion()
    {
        // Arrange
        var assessment = CreateValid();

        // Act
        assessment.Update(
            79,
            180,
            19,
            null,
            "intermediate",
            ActivityLevel.VeryActive,
            "lose weight",
            null,
            null,
            null,
            TestNow.AddMinutes(1)
        );

        // Assert
        Assert.Equal(ActivityLevel.VeryActive, assessment.ActivityLevel);
    }

    [Fact]
    public void Update_AfterSoftDelete_ThrowsDomainException()
    {
        // Arrange
        var assessment = CreateValid();
        assessment.SoftDelete(TestNow);

        // Act & Assert
        var action = () => assessment.Update(
            79,
            180,
            19,
            null,
            "intermediate",
            ActivityLevel.VeryActive,
            "lose weight",
            null,
            null,
            null,
            TestNow.AddMinutes(1)
        );

        Assert.Throws<DomainException>(action);
    }
}
