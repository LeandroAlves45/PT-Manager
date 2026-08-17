using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result> HandleAsync(
        ReactivatePackTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result.Failure(tenant.Error!);

        var outcome = await _store.SetActiveAsync(
            command.PackTypeId,
            tenant.Value,
            true,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToTransitionResult();
    }
}
