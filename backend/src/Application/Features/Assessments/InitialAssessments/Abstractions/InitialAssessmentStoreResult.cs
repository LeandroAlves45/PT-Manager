using Domain.Entities.Assessments;

namespace Application.Features.Assessments.InitialAssessments.Abstractions;

/// <summary>Resultado esperado de uma mutação de avaliação inicial.</summary>
public sealed class InitialAssessmentStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        AlreadyInRequestedState,
        ClientNotFound,
        ClientInactive,
        AssessmentNotFound,
        AssessmentAlreadyExists,
        NotReviewable
    }

    public Status Kind { get; }
    public InitialAssessment? Assessment { get; }

    private InitialAssessmentStoreResult(Status kind, InitialAssessment? assessment)
    {
        Kind = kind;
        Assessment = assessment;
    }

    public static InitialAssessmentStoreResult For(
        Status kind,
        InitialAssessment? assessment = null
    ) => new(kind, assessment);
}

/// <summary>Valores já convertidos para o domínio de uma avaliação inicial a gravar.</summary>
public sealed record InitialAssessmentWriteValues(
    decimal WeightKg,
    int HeightCm,
    decimal? BodyFatPercentage,
    string? MedicalConditions,
    string FitnessLevel,
    Domain.ValueObjects.ActivityLevel ActivityLevel,
    string Goals,
    string? Profession,
    Domain.ValueObjects.BodyMeasurements BodyMeasurements,
    Domain.ValueObjects.NutritionIntake NutritionIntake);
