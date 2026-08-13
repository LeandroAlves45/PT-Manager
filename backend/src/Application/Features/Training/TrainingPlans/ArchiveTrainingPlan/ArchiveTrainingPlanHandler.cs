using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Results;

namespace Application.Features.Training.TrainingPlans.ArchiveTrainingPlan;

/// <summary>Arquiva um plano de treino de forma idempotente.</summary>
public sealed class ArchiveTrainingPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainingPlanStore _store;

    public ArchiveTrainingPlanHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainingPlanStore store)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);

        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        ArchiveTrainingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TrainingPlanId == Guid.Empty)
            return Result.Failure(Error.Validation([
                new ValidationError(
                    "TrainingPlanId",
                    "training_plan_id_required",
                    "Training plan ID is required.")
            ]));

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result.Failure(tenant.Error!);

        var outcome = await _store.ArchiveAsync(
            command.TrainingPlanId,
            tenant.Value,
            _clock.UtcNow,
            cancellationToken);

        return outcome.Kind switch
        {
            TrainingPlanStoreResult.Status.Changed or
                TrainingPlanStoreResult.Status.AlreadyArchived => Result.Success(),
            TrainingPlanStoreResult.Status.NotFound =>
                Result.Failure(TrainingErrors.TrainingPlanNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
