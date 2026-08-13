using Application.Common.Abstractions;
using Application.Errors;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<TrainingPlanDetailsDto>> HandleAsync(
        GetTrainingPlanQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.TrainingPlanId == Guid.Empty)
        {
            return Result<TrainingPlanDetailsDto>.Failure(Error.Validation([
                new ValidationError(
                    "TrainingPlanId",
                    "training_plan_id_required",
                    "Training plan ID is required.")
            ]));
        }

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<TrainingPlanDetailsDto>.Failure(tenant.Error!);

        var details = await _queries.GetDetailsAsync(
            query.TrainingPlanId,
            cancellationToken);
        return details is null
            ? Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanNotFound)
            : Result<TrainingPlanDetailsDto>.Success(details);
    }
}
