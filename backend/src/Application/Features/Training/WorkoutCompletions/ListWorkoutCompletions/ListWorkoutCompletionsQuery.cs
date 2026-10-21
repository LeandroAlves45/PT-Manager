namespace Application.Features.Training.WorkoutCompletions.ListWorkoutCompletions;

/// <summary>Pede uma página das conclusões de treino de um cliente, opcionalmente de um plano.</summary>
public sealed record ListWorkoutCompletionsQuery(
    Guid ClientId,
    Guid? TrainingPlanId = null,
    int PageNumber = 1,
    int PageSize = 50
);
