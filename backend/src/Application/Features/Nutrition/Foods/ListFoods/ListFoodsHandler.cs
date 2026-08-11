using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(foodQueries);
        _validator = validator;
        _tenantContext = tenantContext;
        _foodQueries = foodQueries;
    }

    public async Task<Result<PageResult<FoodDto>>> HandleAsync(
        ListFoodsQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<FoodDto>>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PageResult<FoodDto>>.Failure(tenant.Error!);

        var result = await _foodQueries.ListAsync(
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<FoodDto>>.Success(result);
    }
}
