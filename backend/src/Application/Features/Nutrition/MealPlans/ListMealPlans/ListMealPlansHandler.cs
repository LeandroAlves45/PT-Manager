using Application.Common.Abstractions;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans.ListMealPlans;

/// <summary>Lista planos alimentares do tenant com projeção leve.</summary>
public sealed class ListMealPlansHandler
{
    private readonly IValidator<ListMealPlansQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IMealPlanQueries _mealPlanQueries;

    public ListMealPlansHandler(
        IValidator<ListMealPlansQuery> validator,
        ITenantContext tenantContext,
        IMealPlanQueries mealPlanQueries
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(mealPlanQueries);
        _validator = validator;
        _tenantContext = tenantContext;
        _mealPlanQueries = mealPlanQueries;
    }

    public async Task<Result<PageResult<MealPlanSummaryDto>>> HandleAsync(
        ListMealPlansQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<MealPlanSummaryDto>>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PageResult<MealPlanSummaryDto>>.Failure(tenant.Error!);

        var result = await _mealPlanQueries.ListAsync(
            query.ClientId,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<MealPlanSummaryDto>>.Success(result);
    }
}
