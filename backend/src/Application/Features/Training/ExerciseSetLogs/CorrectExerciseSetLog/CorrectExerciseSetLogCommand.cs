namespace Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;

/// <summary>Solicita a correção de um evento identificado.</summary>
public sealed record CorrectExerciseSetLogCommand(
    Guid ExerciseSetLogId,
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt
);
