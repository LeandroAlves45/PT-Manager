using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Results;

namespace Application.Features.Nutrition.Foods.ArchiveFood;

/// <summary>Arquiva um alimento privado sem afetar MealPlans existentes.</summary>
public sealed class ArchiveFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IFoodStore _foodStore;

    public ArchiveFoodHandler(
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
        ArchiveFoodCommand command,
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
            false,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToTransitionResult();
    }

    private static Error CreateFoodIdRequiredError() => Error.Validation([
        new ValidationError("FoodId", "food_id_required", "Food ID is required.")
    ]);
}
