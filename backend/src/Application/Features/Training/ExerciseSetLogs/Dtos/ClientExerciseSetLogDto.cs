namespace Application.Features.Training.ExerciseSetLogs.Dtos;

/// <summary>Representa uma execução real de uma série prescrita.</summary>
public sealed record ClientExerciseSetLogDto(
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
    DateTime UpdatedAt
);
