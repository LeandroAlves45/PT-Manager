using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.Foods.ReactivateFood;

/// <summary>Reativa um alimento privado de forma idempotente.</summary>
public sealed class ReactivateFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IFoodStore _foodStore;

    public ReactivateFoodHandler(
        ITenantContext tenantContext,
        IClock clock,
        IFoodStore foodStore
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _foodStore = foodStore ?? throw new ArgumentNullException(nameof(foodStore));
    }

    public async Task<Result> HandleAsync(
        ReactivateFoodCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.FoodId == Guid.Empty)
            return Result.Failure(NutritionErrors.FoodIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _foodStore.SetActiveAsync(
            command.FoodId,
            actor.Value.TrainerId,
            true,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToTransitionResult();
    }
}
