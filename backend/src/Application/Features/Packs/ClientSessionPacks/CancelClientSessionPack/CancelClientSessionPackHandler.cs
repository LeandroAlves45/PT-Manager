using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Results;

namespace Application.Features.Packs.ClientSessionPacks.CancelClientSessionPack;

/// <summary>Cancela um pack ainda integral e sem sessões associadas.</summary>
public sealed class CancelClientSessionPackHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSessionPackStore _store;

    public CancelClientSessionPackHandler(
        ITenantContext tenantContext,
        IClock clock,
        IClientSessionPackStore store
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        CancelClientSessionPackCommand command,
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.CancelAsync(
            actor.Value.TrainerId,
            command.ClientSessionPackId,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToCancelResult();
    }
}
