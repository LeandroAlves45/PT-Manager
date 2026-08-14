namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>Regista e corrige eventos sob lock do plano de treino.</summary>
public interface IExerciseSetLogStore
{
    Task<ExerciseSetLogStoreResult> RecordAsync(
        Guid trainerId,
        RecordExerciseSetLogWriteModel model,
        DateTimeOffset currentInstant,
        DateTime now,
        CancellationToken cancellationToken);

    Task<ExerciseSetLogStoreResult> CorrectAsync(
        Guid trainerId,
        CorrectExerciseSetLogWriteModel model,
        DateTimeOffset currentInstant,
        DateTime now,
        CancellationToken cancellationToken);
}
