namespace Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;

/// <summary>Corrige a avaliação inicial identificada.</summary>
public sealed record UpdateInitialAssessmentCommand(
    Guid AssessmentId,
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
