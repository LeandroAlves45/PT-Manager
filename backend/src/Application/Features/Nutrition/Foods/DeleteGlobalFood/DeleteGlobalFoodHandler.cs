using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.Foods.DeleteGlobalFood;

/// <summary>Elimina um alimento global nunca referenciado e preserva o snapshot na auditoria.</summary>
public sealed class DeleteGlobalFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalFoodStore _store;

    public DeleteGlobalFoodHandler(
        ITenantContext tenantContext, IClock clock, IGlobalFoodStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        DeleteGlobalFoodCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FoodId == Guid.Empty)
            return Result.Failure(NutritionErrors.FoodIdRequired());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, NutritionErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.DeleteAsync(
            actor.Value.UserId,
            command.FoodId,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToTransitionResult();
    }
}
