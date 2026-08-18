namespace Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;

/// <summary>Cria a avaliação inicial de um cliente.</summary>
public sealed record CreateInitialAssessmentCommand(
    Guid ClientId,
    decimal WeightKg,
    int HeightCm,
    decimal? BodyFatPercentage,
    string? MedicalConditions,
    string FitnessLevel,
    string ActivityLevel,
    string Goals,
    string? Profession,
    AssessmentValueInput.BodyMeasurements? BodyMeasurements,
    AssessmentValueInput.NutritionIntake? NutritionIntake
);
