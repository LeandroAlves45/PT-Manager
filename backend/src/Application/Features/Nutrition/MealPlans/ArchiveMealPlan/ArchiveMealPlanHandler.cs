using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.MealPlans.ArchiveMealPlan;

/// <summary>Arquiva um plano alimentar de forma idempotente.</summary>
public sealed class ArchiveMealPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMealPlanStore _mealPlanStore;

    public ArchiveMealPlanHandler(
        ITenantContext tenantContext,
        IClock clock,
        IMealPlanStore mealPlanStore
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _mealPlanStore = mealPlanStore ?? throw new ArgumentNullException(nameof(mealPlanStore));
    }

    public async Task<Result> HandleAsync(
        ArchiveMealPlanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.MealPlanId == Guid.Empty)
            return Result.Failure(NutritionErrors.MealPlanIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.MealPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _mealPlanStore.SetArchivedAsync(
            command.MealPlanId,
            actor.Value.TrainerId,
            true,
            _clock.UtcNow,
            cancellationToken
        );
        return outcome.ToTransitionResult();
    }
}
