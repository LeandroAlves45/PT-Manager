using Application.Features.Assessments.CheckIns.Dtos;

namespace Api.Contracts.Assessments;

/// <summary>Agenda um check-in para um cliente.</summary>
public sealed record CreateCheckInRequest(
    Guid ClientId,
    DateOnly CheckInDate,
    DateOnly? TargetDate);

/// <summary>Move um check-in agendado para outra data.</summary>
public sealed record RescheduleCheckInRequest(
    DateOnly CheckInDate,
    DateOnly? TargetDate);

/// <summary>
/// Corpo de resposta a um check-in, submetido pelo cliente ou corrigido pelo trainer.
/// </summary>
public sealed record CheckInAnswerRequest(
    decimal WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    BodyMeasurementsPayload? BodyMeasurements,
    CheckInFeedbackPayload? Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore);

/// <summary>Correção de um check-in pelo personal trainer.</summary>
public sealed record CorrectCheckInRequest(
    DateOnly? TargetDate,
    decimal WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    BodyMeasurementsPayload? BodyMeasurements,
    CheckInFeedbackPayload? Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore);

/// <summary>Check-in completo, na perspetiva do personal trainer.</summary>
public sealed record CheckInResponse(
    Guid Id,
    Guid ClientId,
    DateOnly CheckInDate,
    DateOnly? TargetDate,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    BodyMeasurementsPayload BodyMeasurements,
    CheckInFeedbackPayload Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore,
    string Status,
    DateTime? RespondedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da Api.</summary>
    public static CheckInResponse From(CheckInDto checkIn)
    {
        ArgumentNullException.ThrowIfNull(checkIn);

        return new(
            checkIn.Id,
            checkIn.ClientId,
            checkIn.CheckInDate,
            checkIn.TargetDate,
            checkIn.WeightKg,
            checkIn.BodyFatPercentage,
            checkIn.Notes,
            BodyMeasurementsPayload.From(checkIn.BodyMeasurements),
            CheckInFeedbackPayload.From(checkIn.Feedback),
            checkIn.TrainingAdherenceScore,
            checkIn.NutritionAdherenceScore,
            checkIn.Status,
            checkIn.RespondedAt,
            checkIn.CancelledAt,
            checkIn.CreatedAt,
            checkIn.UpdatedAt);
    }
}
