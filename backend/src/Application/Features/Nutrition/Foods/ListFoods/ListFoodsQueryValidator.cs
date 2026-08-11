using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.Foods.ListFoods;

/// <summary>Valida pesquisa, filtro e paginação de Food.</summary>
public sealed class ListFoodsQueryValidator : AbstractValidator<ListFoodsQuery>
{
    public ListFoodsQueryValidator()
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
