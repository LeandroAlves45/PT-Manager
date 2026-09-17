namespace Application.Features.Training.WorkoutCompletions.CompleteMyWorkout;

/// <summary>Conclui hoje um dia do plano ativo; aceita treino parcial.</summary>
public sealed record CompleteMyWorkoutCommand(
    Guid TrainingPlanDayId,
    string? Notes);
