using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.ListGlobalFoods;

/// <summary>Valida pesquisa e paginação administrativa.</summary>
public sealed class ListGlobalFoodsQueryValidator : AbstractValidator<ListGlobalFoodsQuery>
{
    public ListGlobalFoodsQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(255)
            .WithErrorCode("food_search_too_long");

        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("food_activity_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
