using Domain.ValueObjects;

namespace Application.Features.Assessments.InitialAssessments.Abstractions;

/// <summary>Persiste a avaliação inicial com isolamento de tenant.</summary>
public interface IInitialAssessmentStore
{
    Task<InitialAssessmentStoreResult> CreateAsync(
        Guid trainerId,
        Guid clientId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<InitialAssessmentStoreResult> UpdateAsync(
        Guid trainerId,
        Guid assessmentId,
        decimal weightKg,
        int heightCm,
        decimal? bodyFatPercentage,
        string? medicalConditions,
        string fitnessLevel,
        ActivityLevel activityLevel,
        string goals,
        string? profession,
        BodyMeasurements bodyMeasurements,
        NutritionIntake nutritionIntake,
        DateTime now,
        CancellationToken cancellationToken
    );
}
