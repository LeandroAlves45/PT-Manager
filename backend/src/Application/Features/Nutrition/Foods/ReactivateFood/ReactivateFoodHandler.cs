using Application.Common.Abstractions;
using Application.Errors;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(foodStore);
        _tenantContext = tenantContext;
        _clock = clock;
        _foodStore = foodStore;
    }

    public async Task<Result> HandleAsync(
        ReactivateFoodCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.FoodId == Guid.Empty)
            return Result.Failure(CreateFoodIdRequiredError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result.Failure(tenant.Error!);

        var outcome = await _foodStore.SetActiveAsync(
            command.FoodId,
            tenant.Value,
            true,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.Kind switch
        {
            FoodStoreResult.Status.Changed => Result.Success(),
            FoodStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            FoodStoreResult.Status.NotFound => Result.Failure(NutritionErrors.FoodNotFound),
            FoodStoreResult.Status.GlobalReadOnly => Result.Failure(NutritionErrors.GlobalFoodReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }

    private static Error CreateFoodIdRequiredError() => Error.Validation([
        new ValidationError("FoodId", "food_id_required", "Food ID is required.")
    ]);
}
