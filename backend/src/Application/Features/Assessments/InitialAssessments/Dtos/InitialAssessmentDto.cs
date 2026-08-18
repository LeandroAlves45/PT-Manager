namespace Application.Features.Assessments.InitialAssessments.Dtos;

/// <summary>Dados completos da avaliação inicial de um cliente.</summary>
public sealed record InitialAssessmentDto(
    Guid Id,
    Guid ClientId,
    decimal WeightKg,
    int HeightCm,
    decimal? BodyFatPercentage,
    string? MedicalConditions,
    string FitnessLevel,
    string ActivityLevel,
    string Goals,
    string? Profession,
    AssessmentValueDto.BodyMeasurements BodyMeasurements,
    AssessmentValueDto.NutritionIntake NutritionIntake,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
