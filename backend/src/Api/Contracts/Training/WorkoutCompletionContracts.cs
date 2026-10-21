using Application.Features.Training.WorkoutCompletions.Dtos;

namespace Api.Contracts.Training;

/// <summary>
/// Conclusão de treino de um cliente, vista pelo personal trainer. <c>local_date</c> é o dia no fuso do
/// personal trainer; <c>notes</c> é a nota do cliente, só de leitura.
/// </summary>
public sealed record WorkoutCompletionResponse(
    Guid Id,
    Guid ClientId,
    Guid TrainingPlanId,
    Guid TrainingPlanDayId,
    DateOnly LocalDate,
    string? Notes,
    DateTime CompletedAt)
{
    /// <summary>Projeta a conclusão da Application.</summary>
    public static WorkoutCompletionResponse From(WorkoutCompletionDto completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        return new WorkoutCompletionResponse(
            completion.Id,
            completion.ClientId,
            completion.TrainingPlanId,
            completion.TrainingPlanDayId,
            completion.LocalDate,
            completion.Notes,
            completion.CompletedAt);
    }
}
