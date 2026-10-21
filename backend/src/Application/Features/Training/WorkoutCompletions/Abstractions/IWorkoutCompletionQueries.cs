using Application.Features.Training.WorkoutCompletions.Dtos;
using Application.Pagination;

namespace Application.Features.Training.WorkoutCompletions.Abstractions;

/// <summary>
/// Leitura das conclusões de treino do tenant efetivo (filtro global por personal trainer).
/// </summary>
public interface IWorkoutCompletionQueries
{
    /// <summary>Conclusões de um cliente, da mais recente para a mais antiga.</summary>
    Task<PageResult<WorkoutCompletionDto>> ListAsync(
        Guid clientId,
        Guid? trainingPlanId,
        PageRequest page,
        CancellationToken cancellationToken);
}
