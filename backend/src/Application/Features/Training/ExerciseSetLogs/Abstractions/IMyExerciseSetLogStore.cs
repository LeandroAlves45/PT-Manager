namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>
/// Regista, corrige e remove séries do próprio cliente sob lock do plano de treino.
/// O cliente é resolvido pelo utilizador autenticado; o dia local vem do fuso do personal trainer.
/// </summary>
public interface IMyExerciseSetLogStore
{
    Task<MyExerciseSetLogStoreResult> RecordAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanDayExerciseId,
        int setNumber,
        decimal weightKg,
        int repsDone,
        decimal? rpe,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken);

    Task<MyExerciseSetLogStoreResult> CorrectAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid exerciseSetLogId,
        decimal weightKg,
        int repsDone,
        decimal? rpe,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken);

    Task<MyExerciseSetLogStoreResult> DeleteAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid exerciseSetLogId,
        DateTime now,
        CancellationToken cancellationToken);
}
