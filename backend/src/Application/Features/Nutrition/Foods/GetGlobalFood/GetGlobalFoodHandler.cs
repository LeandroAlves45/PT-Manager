using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;

namespace Application.Features.Nutrition.Foods.GetGlobalFood;

/// <summary>Obtém exclusivamente um alimento global.</summary>
public sealed class GetGlobalFoodHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IGlobalFoodQueries _queries;

    public GetGlobalFoodHandler(
        ITenantContext tenantContext,
        IGlobalFoodQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<GlobalFoodDto>> HandleAsync(
        GetGlobalFoodQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FoodId == Guid.Empty)
            return Result<GlobalFoodDto>.Failure(NutritionErrors.FoodIdRequired());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, NutritionErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<GlobalFoodDto>.Failure(actor.Error!);

        var food = await _queries.GetAsync(query.FoodId, cancellationToken);

        return food is null
            ? Result<GlobalFoodDto>.Failure(NutritionErrors.FoodNotFound)
            : Result<GlobalFoodDto>.Success(food);
    }
}
