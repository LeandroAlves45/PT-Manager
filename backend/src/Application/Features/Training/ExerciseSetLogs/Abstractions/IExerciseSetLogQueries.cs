using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Pagination;

namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>Executa listagens projetadas de logs de treino.</summary>
public interface IExerciseSetLogQueries
{
    Task<ClientExerciseSetLogDto?> GetAsync(
        Guid exerciseSetLogId,
        CancellationToken cancellationToken);

    Task<PageResult<ClientExerciseSetLogDto>> ListAsync(
        Guid clientId,
        Guid? trainingPlanId,
        DateTimeOffset? performedFrom,
        DateTimeOffset? performedTo,
        PageRequest page,
        CancellationToken cancellationToken);
}
