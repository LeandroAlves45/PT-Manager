using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.ListGlobalFoods;

/// <summary>Lista alimentos globais para um superuser autorizado.</summary>
public sealed class ListGlobalFoodsHandler
{
    private readonly IValidator<ListGlobalFoodsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IGlobalFoodQueries _queries;

    public ListGlobalFoodsHandler(
        IValidator<ListGlobalFoodsQuery> validator,
        ITenantContext tenantContext,
        IGlobalFoodQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<GlobalFoodDto>>> HandleAsync(
        ListGlobalFoodsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<GlobalFoodDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, NutritionErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<GlobalFoodDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<GlobalFoodDto>>.Success(page);
    }
}
