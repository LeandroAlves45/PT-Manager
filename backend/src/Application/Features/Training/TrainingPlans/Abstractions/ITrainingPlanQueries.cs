using Application.Features.Training.TrainingPlans.Dtos;
using Application.Features.Training.TrainingPlans.ListTrainingPlans;
using Application.Pagination;

namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Executa leituras read-only de planos de treino.</summary>
public interface ITrainingPlanQueries
{
    Task<TrainingPlanDetailsDto?> GetDetailsAsync(
        Guid trainingPlanId,
        CancellationToken cancellationToken
    );

    Task<PageResult<TrainingPlanSummaryDto>> ListAsync(
        Guid? clientId,
        string? search,
        TrainingPlanActivityFilter activity,
        DateOnly? endsFrom,
        DateOnly? endsTo,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
