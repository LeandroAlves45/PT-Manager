using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.PackTypes.ListPackTypes;

/// <summary>Valida filtros e paginação de listPackTypesQuery.</summary>
public sealed class ListPackTypesQueryValidator : AbstractValidator<ListPackTypesQuery>
{
    public ListPackTypesQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(255)
            .WithErrorCode("pack_type_search_too_long");

        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("pack_type_activity_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
