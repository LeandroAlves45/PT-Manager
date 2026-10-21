namespace Application.Features.Training.WorkoutCompletions.Dtos;

/// <summary>
/// Conclusão de treino de um cliente, vista pelo trainer dono (U63). A nota é a que o cliente
/// escreveu ao concluir; é só de leitura e pode ser nula.
/// </summary>
public sealed record WorkoutCompletionDto(
    Guid Id,
    Guid ClientId,
    Guid TrainingPlanId,
    Guid TrainingPlanDayId,
    DateOnly LocalDate,
    string? Notes,
    DateTime CompletedAt);
