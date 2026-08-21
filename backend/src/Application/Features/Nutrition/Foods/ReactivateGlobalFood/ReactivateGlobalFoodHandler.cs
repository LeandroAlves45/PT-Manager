using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.Foods.ReactivateGlobalFood;

/// <summary>Reativa um alimento global de forma auditada.</summary>
public sealed class ReactivateGlobalFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalFoodStore _store;

    public ReactivateGlobalFoodHandler(
        ITenantContext tenantContext, IClock clock, IGlobalFoodStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ReactivateGlobalFoodCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FoodId == Guid.Empty)
            return Result.Failure(NutritionErrors.FoodIdRequired());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, NutritionErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.SetActiveAsync(
            actor.Value.UserId,
            command.FoodId,
            true,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToTransitionResult();
    }
}
