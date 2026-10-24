using Application.Features.Assessments;
using Application.Features.Assessments.InitialAssessments;

namespace Application.Features.ClientPortal.SubmitMyInitialAssessment;

/// <summary>Avaliação inicial preenchida pelo próprio cliente, com os campos do personal trainer.</summary>
public sealed record SubmitMyInitialAssessmentCommand(
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
) : IInitialAssessmentValues;
