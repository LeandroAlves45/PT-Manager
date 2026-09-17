namespace Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;

/// <summary>
/// Regista uma série executada hoje pelo cliente autenticado. O instante é o do servidor;
/// o cliente não escolhe a data.
/// </summary>
public sealed record RecordMyExerciseSetLogCommand(
    Guid TrainingPlanDayExerciseId,
    int SetNumber,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    string? Notes);
