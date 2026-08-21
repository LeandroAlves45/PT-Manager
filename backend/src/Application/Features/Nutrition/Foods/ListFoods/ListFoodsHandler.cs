using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.ListFoods;

/// <summary>Lista o catálogo visível com paginação estável.</summary>
public sealed class ListFoodsHandler
{
    private readonly IValidator<ListFoodsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IFoodQueries _foodQueries;

    public ListFoodsHandler(
        IValidator<ListFoodsQuery> validator,
        ITenantContext tenantContext,
        IFoodQueries foodQueries
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _foodQueries = foodQueries ?? throw new ArgumentNullException(nameof(foodQueries));
    }

    public async Task<Result<PageResult<FoodDto>>> HandleAsync(
        ListFoodsQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<FoodDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<FoodDto>>.Failure(actor.Error!);

        var result = await _foodQueries.ListAsync(
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<FoodDto>>.Success(result);
    }
}
