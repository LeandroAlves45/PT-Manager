using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _mealPlanQueries = mealPlanQueries ?? throw new ArgumentNullException(nameof(mealPlanQueries));
    }

    public async Task<Result<PageResult<MealPlanSummaryDto>>> HandleAsync(
        ListMealPlansQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<MealPlanSummaryDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NutritionErrors.MealPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<MealPlanSummaryDto>>.Failure(actor.Error!);

        var result = await _mealPlanQueries.ListAsync(
            query.ClientId,
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<MealPlanSummaryDto>>.Success(result);
    }
}
