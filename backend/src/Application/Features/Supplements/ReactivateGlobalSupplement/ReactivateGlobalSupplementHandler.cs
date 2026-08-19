using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Results;

namespace Application.Features.Supplements.ReactivateGlobalSupplement;

/// <summary>Reactivate um suplemento global de forma auditada.</summary>
public sealed class ReactivateGlobalSupplementHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalSupplementStore _store;

    public ReactivateGlobalSupplementHandler(
        ITenantContext tenantContext, IClock clock, IGlobalSupplementStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ReactivateGlobalSupplementCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SupplementId == Guid.Empty)
            return Result.Failure(SupplementErrors.SupplementIdRequired);

        var actor = SupplementActorAuthorization.RequireAdministrator(_tenantContext);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.SetActiveAsync(
            actor.Value.UserId,
            command.SupplementId,
            true,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToTransitionResult();
    }
}
