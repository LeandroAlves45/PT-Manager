namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>Transporta os valores corrigíveis de um evento.</summary>
public sealed record CorrectExerciseSetLogWriteModel(
    Guid ExerciseSetLogId,
    decimal WeightKg,
    int RepsDone,
    string? Notes,
    DateTimeOffset PerformedAt
);
