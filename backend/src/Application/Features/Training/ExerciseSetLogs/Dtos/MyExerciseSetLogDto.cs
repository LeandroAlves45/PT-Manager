namespace Application.Features.Training.ExerciseSetLogs.Dtos;

/// <summary>Série registada pelo próprio cliente, sem identificadores de tenant ou cliente.</summary>
public sealed record MyExerciseSetLogDto(
    Guid Id,
    Guid TrainingPlanDayExerciseId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    string? Notes,
    DateTimeOffset PerformedAt,
    DateTime UpdatedAt);
