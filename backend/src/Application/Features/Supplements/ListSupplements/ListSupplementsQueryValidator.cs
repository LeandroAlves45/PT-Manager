using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListSupplements;

/// <summary>Valida pesquisa, atividade e paginação.</summary>
public sealed class ListSupplementsQueryValidator : AbstractValidator<ListSupplementsQuery>
{
    public ListSupplementsQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(255)
            .WithErrorCode("supplement_search_too_long");

        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("supplement_activity_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
