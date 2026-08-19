using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListMySupplementAssignments;

/// <summary>Valida paginação da lista client-only.</summary>
public sealed class ListMySupplementAssignmentsQueryValidator
    : AbstractValidator<ListMySupplementAssignmentsQuery>
{
    public ListMySupplementAssignmentsQueryValidator()
    {
        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
