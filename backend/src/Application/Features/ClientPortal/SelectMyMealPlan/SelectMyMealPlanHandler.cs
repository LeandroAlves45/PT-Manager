using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Results;

namespace Application.Features.ClientPortal.SelectMyMealPlan;

/// <summary>Plano alimentar que o cliente quer passar a seguir.</summary>
public sealed record SelectMyMealPlanCommand(Guid MealPlanId);

/// <summary>
/// Guarda na conta o plano alimentar que o cliente escolheu. Fica até ele escolher outro, e
/// outro aparelho abre o mesmo. Plano de outro cliente, inexistente ou arquivado responde o
/// mesmo NotFound, sem revelar qual das condições falhou.
/// </summary>
public sealed class SelectMyMealPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IMyMealPlanSelectionStore _store;

    public SelectMyMealPlanHandler(ITenantContext tenantContext, IMyMealPlanSelectionStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        SelectMyMealPlanCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        if (command.MealPlanId == Guid.Empty)
            return Result.Failure(ClientPortalErrors.MealPlanNotFound);

        var selected = await _store.SelectAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.MealPlanId,
            cancellationToken);

        return selected ? Result.Success() : Result.Failure(ClientPortalErrors.MealPlanNotFound);
    }
}
