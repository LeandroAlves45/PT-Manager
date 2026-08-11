using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;

namespace Application.Features.Nutrition.Foods.GetFood;

/// <summary>Obtém um alimento visível sem tracking.</summary>
public sealed class GetFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IFoodQueries _foodQueries;

    public GetFoodHandler(
        ITenantContext tenantContext,
        IFoodQueries foodQueries
    )
    {
        _tenantContext = tenantContext;
        _foodQueries = foodQueries;
    }

    public async Task<Result<FoodDto>> HandleAsync(
        GetFoodQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.FoodId == Guid.Empty)
        {
            return Result<FoodDto>.Failure(Error.Validation([
                new ValidationError("FoodId", "food_id_required", "Food ID is required.")
            ]));
        }

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<FoodDto>.Failure(tenant.Error!);

        var food = await _foodQueries.GetAsync(query.FoodId, cancellationToken);
        return food is null
            ? Result<FoodDto>.Failure(NutritionErrors.FoodNotFound)
            : Result<FoodDto>.Success(food);
    }
}
