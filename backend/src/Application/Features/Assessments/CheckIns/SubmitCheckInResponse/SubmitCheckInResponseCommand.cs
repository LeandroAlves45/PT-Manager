namespace Application.Features.Assessments.CheckIns.SubmitCheckInResponse;

/// <summary>Submete a resposta do cliente autenticado ao CheckIn indicado.</summary>
public sealed record SubmitCheckInResponseCommand(
    Guid CheckInId,
    decimal WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    AssessmentValueInput.BodyMeasurements? BodyMeasurements,
    AssessmentValueInput.CheckInFeedback? Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore
);
