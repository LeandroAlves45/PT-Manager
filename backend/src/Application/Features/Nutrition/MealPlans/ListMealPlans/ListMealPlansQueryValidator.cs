using Application.Validation;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans.ListMealPlans;

/// <summary>Valida filtros da listagem paginada de planos alimentares.</summary>
public sealed class ListMealPlansQueryValidator : AbstractValidator<ListMealPlansQuery>
{
    public ListMealPlansQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .Must(clientId => !clientId.HasValue || clientId.Value != Guid.Empty)
            .WithErrorCode("meal_plan_client_id_invalid");

        RuleFor(query => query.Search)
            .MaximumLength(255)
            .WithErrorCode("meal_plan_search_too_long");

        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("meal_plan_activity_invalid");

        RuleFor(query => query.EndsFrom)
            .LessThanOrEqualTo(query => query.EndsTo!.Value)
            .When(query => query.EndsFrom.HasValue && query.EndsTo.HasValue)
            .WithErrorCode("meal_plan_ends_range_invalid")
            .WithMessage("EndsFrom cannot be after EndsTo");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
