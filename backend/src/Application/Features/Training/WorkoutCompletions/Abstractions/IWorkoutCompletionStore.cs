namespace Application.Features.Training.WorkoutCompletions.Abstractions;

/// <summary>Regista a conclusão do treino do dia sob lock do plano de treino.</summary>
public interface IWorkoutCompletionStore
{
    Task<WorkoutCompletionStoreResult> CompleteAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanDayId,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken);
}
