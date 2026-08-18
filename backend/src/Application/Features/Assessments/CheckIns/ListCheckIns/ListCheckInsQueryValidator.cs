using Application.Validation;
using FluentValidation;

namespace Application.Features.Assessments.CheckIns.ListCheckIns;

/// <summary>Valida a listagem de check-ins.</summary>
public sealed class ListCheckInsQueryValidator : AbstractValidator<ListCheckInsQuery>
{
    public ListCheckInsQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithErrorCode("client_id_invalid");

        RuleFor(query => query)
            .Must(query => !query.FromDate.HasValue || !query.ToDate.HasValue ||
                query.FromDate.Value <= query.ToDate.Value)
            .WithName("ToDate")
            .WithErrorCode("check_in_date_range_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
