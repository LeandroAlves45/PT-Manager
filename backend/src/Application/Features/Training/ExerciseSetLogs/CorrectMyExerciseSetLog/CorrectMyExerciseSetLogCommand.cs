namespace Application.Features.Training.ExerciseSetLogs.CorrectMyExerciseSetLog;

/// <summary>Corrige valores de uma série própria registada hoje; o instante mantém-se.</summary>
public sealed record CorrectMyExerciseSetLogCommand(
    Guid ExerciseSetLogId,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    string? Notes);
