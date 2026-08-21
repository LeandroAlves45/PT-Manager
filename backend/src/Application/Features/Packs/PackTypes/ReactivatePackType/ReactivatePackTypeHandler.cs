using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Results;

namespace Application.Features.Packs.PackTypes.ReactivatePackType;

/// <summary>Reativa um tipo de pack do tenant.</summary>
public sealed class ReactivatePackTypeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPackTypeStore _store;

    public ReactivatePackTypeHandler(
        ITenantContext tenantContext,
        IClock clock,
        IPackTypeStore store
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ReactivatePackTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.SetActiveAsync(
            command.PackTypeId,
            actor.Value.TrainerId,
            true,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToTransitionResult();
    }
}
