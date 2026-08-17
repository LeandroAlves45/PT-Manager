using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        CancelClientSessionPackCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result.Failure(tenant.Error!);

        var outcome = await _store.CancelAsync(
            tenant.Value,
            command.ClientSessionPackId,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToCancelResult();
    }
}
