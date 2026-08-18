namespace Application.Features.Assessments.CheckIns.Dtos;

/// <summary>Representa um CheckIn sem expor o tenant.</summary>
public sealed record CheckInDto(
    Guid Id,
    Guid ClientId,
    DateOnly CheckInDate,
    DateOnly? TargetDate,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    AssessmentValueDto.BodyMeasurements BodyMeasurements,
    AssessmentValueDto.CheckInFeedback Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore,
    string Status,
    DateTime? RespondedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
