using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Results;

namespace Application.Features.Training.TrainingPlans.GetTrainingPlan;

/// <summary>Obtém um plano de treino visível ou devolve NotFound seguro.</summary>
public sealed class GetTrainingPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITrainingPlanQueries _queries;

    public GetTrainingPlanHandler(
        ITenantContext tenantContext,
        ITrainingPlanQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<TrainingPlanDetailsDto>> HandleAsync(
        GetTrainingPlanQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TrainingPlanId == Guid.Empty)
            return Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainingPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainingPlanDetailsDto>.Failure(actor.Error!);

        var details = await _queries.GetDetailsAsync(
            query.TrainingPlanId,
            cancellationToken);
        return details is null
            ? Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanNotFound)
            : Result<TrainingPlanDetailsDto>.Success(details);
    }
}
