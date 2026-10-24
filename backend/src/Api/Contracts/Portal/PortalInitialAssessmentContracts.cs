using Api.Contracts.Assessments;
using Application.Features.ClientPortal.GetMyInitialAssessment;

namespace Api.Contracts.Portal;

/// <summary>Avaliação inicial preenchida pelo cliente, com os mesmos campos do personal trainer.</summary>
public sealed record SubmitMyInitialAssessmentRequest(
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

/// <summary>Se a avaliação inicial ainda falta. Não traz os dados da avaliação.</summary>
public sealed record MyInitialAssessmentStatusResponse(bool Pending)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static MyInitialAssessmentStatusResponse From(MyInitialAssessmentStatusDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyInitialAssessmentStatusResponse(dto.Pending);
    }
}
