using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ArchiveTrainingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TrainingPlanId == Guid.Empty)
            return Result.Failure(TrainingErrors.TrainingPlanIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainingPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.ArchiveAsync(
            command.TrainingPlanId,
            actor.Value.TrainerId,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToArchiveResult();
    }
}
