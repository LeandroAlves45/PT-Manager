using Domain.Entities.Assessments;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Assessments;

public sealed class InitialAssessmentTests
{
    private static readonly DateTime TestNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    private static InitialAssessment CreateValid(
        string? profession = null,
        BodyMeasurements? bodyMeasurements = null,
        NutritionIntake? nutritionIntake = null
    ) =>
        new(
            ownerTrainerId: Guid.NewGuid(),
            clientId: Guid.NewGuid(),
            age: 30,
            gender: "male",
            weightKg: 80,
            heightCm: 180,
            bodyFatPercentage: 20,
            medicalConditions: null,
            fitnessLevel: "moderately_active",
            goals: "lose weight",
            profession: profession,
            bodyMeasurements: bodyMeasurements,
            nutritionIntake: nutritionIntake,
            now: TestNow
        );

    private static NutritionIntake CreateNutritionIntake(
        int? sleepQuality = null) =>
        new(
            null, null, null, null, null, null,
            sleepQuality: sleepQuality,
            null, null, null, null, null, null, null
        );

    [Fact]
    public void Constructor_ProfessionWithinLimit_Accepted()
    {
        // Arrange
        var profession = new string('a', 255);

        // Act
        var assessment = CreateValid(profession: profession);

        // Assert
        Assert.Equal(profession, assessment.Profession);
    }

    [Fact]
    public void Constructor_ProfessionExceedsLimit_ThrowsException()
    {
        // Arrange
        var profession = new string('a', 256);

        // Act & Assert
        Assert.Throws<DomainException>(() => CreateValid(profession: profession));
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
            age: 30,
            gender: "male",
            weightKg: 80,
            heightCm: 180,
            bodyFatPercentage: null,
            medicalConditions: null,
            fitnessLevel: "moderately_active",
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
    public void Constructor_MeasurementsNull_DefaultsToEmpty()
    {
        // Act
        var assessment = CreateValid(bodyMeasurements: null);

        // Assert
        Assert.Equal(BodyMeasurements.Empty, assessment.BodyMeasurements);
    }

    [Fact]
    public void Constructor_NutritionIntakeNull_DefaultsToEmpty()
    {
        // Act
        var assessment = CreateValid(nutritionIntake: null);

        // Assert
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
                age: 30,
                gender: "male",
                weightKg: 80,
                heightCm: 180,
                bodyFatPercentage: null,
                medicalConditions: null,
                fitnessLevel: "moderately_active",
                goals: "lose weight",
                profession: null,
                bodyMeasurements: null,
                nutritionIntake: null,
                now: TestNow.AddMinutes(1)
            )
        );
    }
}
