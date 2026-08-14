namespace Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;

/// <summary>Solicita o registo de uma série executada.</summary>
public sealed record RecordExerciseSetLogCommand(
    Guid TrainingPlanDayExerciseId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt
);
