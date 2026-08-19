using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Results;

namespace Application.Features.Supplements.ReactivateSupplement;

/// <summary>Reativa um suplemento privado do tenant.</summary>
public sealed class ReactivateSupplementHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISupplementStore _store;

    public ReactivateSupplementHandler(
        ITenantContext tenantContext,
        IClock clock,
        ISupplementStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ReactivateSupplementCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SupplementId == Guid.Empty)
            return Result.Failure(SupplementErrors.SupplementIdRequired);

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.SetActiveAsync(
            actor.Value.TrainerId,
            command.SupplementId,
            true,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToTransitionResult();
    }
}
