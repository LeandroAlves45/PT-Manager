using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListGlobalSupplements;

/// <summary>Valida pesquisa e paginação administrativa.</summary>
public sealed class ListGlobalSupplementsQueryValidator
    : AbstractValidator<ListGlobalSupplementsQuery>
{
    public ListGlobalSupplementsQueryValidator()
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
