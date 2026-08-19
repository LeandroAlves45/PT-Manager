using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListSupplementAssignments;

/// <summary>Valida filtros e paginação de atribuições.</summary>
public sealed class ListSupplementAssignmentsQueryValidator
    : AbstractValidator<ListSupplementAssignmentsQuery>
{
    public ListSupplementAssignmentsQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .Must(value => !value.HasValue || value.Value != Guid.Empty)
            .WithErrorCode("client_id_required");

        RuleFor(query => query.Activity).IsInEnum()
            .WithErrorCode("supplement_assignment_activity_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
