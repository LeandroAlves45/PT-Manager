namespace Application.Features.Training.WorkoutCompletions.Dtos;

/// <summary>Conclusão de treino do cliente autenticado.</summary>
public sealed record MyWorkoutCompletionDto(
    Guid Id,
    Guid TrainingPlanId,
    Guid TrainingPlanDayId,
    DateOnly LocalDate,
    string? Notes,
    DateTime CompletedAt);
