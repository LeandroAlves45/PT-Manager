namespace Application.Features.Assessments.CheckIns.CorrectCheckIn;

/// <summary>Corrige os dados de um check-in respondido.</summary>
public sealed record CorrectCheckInCommand(
    Guid CheckInId,
    DateOnly? TargetDate,
    decimal WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    AssessmentValueInput.BodyMeasurements? BodyMeasurements,
    AssessmentValueInput.CheckInFeedback? Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore
);
