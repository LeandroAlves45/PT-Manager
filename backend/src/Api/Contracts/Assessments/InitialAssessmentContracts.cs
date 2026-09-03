using Application.Features.Assessments.InitialAssessments.Dtos;

namespace Api.Contracts.Assessments;

/// <summary>Cria a avaliação inicial de um cliente.</summary>
public sealed record CreateInitialAssessmentRequest(
    Guid ClientId,
    decimal WeightKg,
    int HeightCm,
    decimal? BodyFatPercentage,
    string? MedicalConditions,
    string FitnessLevel,
    string ActivityLevel,
    string Goals,
    string? Profession,
    BodyMeasurementsPayload? BodyMeasurements,
    NutritionIntakePayload? NutritionIntake);

/// <summary>Substitui os campos editáveis de uma avaliação inicial existente.</summary>
public sealed record UpdateInitialAssessmentRequest(
    decimal WeightKg,
    int HeightCm,
    decimal? BodyFatPercentage,
    string? MedicalConditions,
    string FitnessLevel,
    string ActivityLevel,
    string Goals,
    string? Profession,
    BodyMeasurementsPayload? BodyMeasurements,
    NutritionIntakePayload? NutritionIntake);

/// <summary>Avaliação inicial completa, visível ao personal trainer.</summary>
public sealed record InitialAssessmentResponse(
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
    BodyMeasurementsPayload BodyMeasurements,
    NutritionIntakePayload NutritionIntake,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da Api.</summary>
    public static InitialAssessmentResponse From(InitialAssessmentDto assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return new(
            assessment.Id,
            assessment.ClientId,
            assessment.WeightKg,
            assessment.HeightCm,
            assessment.BodyFatPercentage,
            assessment.MedicalConditions,
            assessment.FitnessLevel,
            assessment.ActivityLevel,
            assessment.Goals,
            assessment.Profession,
            BodyMeasurementsPayload.From(assessment.BodyMeasurements),
            NutritionIntakePayload.From(assessment.NutritionIntake),
            assessment.CreatedAt,
            assessment.UpdatedAt);
    }
}
