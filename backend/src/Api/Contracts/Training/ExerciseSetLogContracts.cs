using Application.Features.Training.ExerciseSetLogs.Dtos;

namespace Api.Contracts.Training;

/// <summary>Regista uma série efetivamente realizada.</summary>
public sealed record RegisterExerciseSetLogRequest(
    Guid TrainingPlanDayExerciseId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt);

/// <summary>Corrige o registo de série já existente.</summary>
public sealed record CorrectExerciseSetLogRequest(
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt);

/// <summary>Registo de série executada por um cliente.</summary>
public sealed record ExerciseSetLogResponse(
    Guid Id,
    Guid ClientId,
    Guid TrainingPlanId,
    Guid TrainingPlanDayId,
    Guid TrainingPlanDayExerciseId,
    Guid ExerciseId,
    string ExerciseName,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o registo da Application.</summary>
    public static ExerciseSetLogResponse From(ClientExerciseSetLogDto log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return new(
            log.Id,
            log.ClientId,
            log.TrainingPlanId,
            log.TrainingPlanDayId,
            log.TrainingPlanDayExerciseId,
            log.ExerciseId,
            log.ExerciseName,
            log.SetNumber,
            log.WeightKg,
            log.RepsDone,
            log.Notes,
            log.PerformedAt,
            log.CreatedAt,
            log.UpdatedAt);
    }
}
