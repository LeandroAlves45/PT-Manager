namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>Transporta os dados executados para a store transacional.</summary>
public sealed record RecordExerciseSetLogWriteModel(
    Guid TrainingPlanDayExerciseId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt
);
