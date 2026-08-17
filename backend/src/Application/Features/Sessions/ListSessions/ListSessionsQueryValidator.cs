using Application.Validation;
using FluentValidation;

namespace Application.Features.Sessions.ListSessions;

/// <summary>Valida filtros da listagem de sessões.</summary>
public sealed class ListSessionsQueryValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsQueryValidator()
    {
        RuleFor(query => query.ClientId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithErrorCode("client_id_invalid");

        RuleFor(query => query)
            .Must(query => !query.StartsFrom.HasValue ||
                !query.StartsBefore.HasValue ||
                query.StartsFrom.Value < query.StartsBefore.Value)
            .WithName("StartsBefore")
            .WithErrorCode("session_date_range_invalid")
            .WithMessage("Starts before must be later than starts from.");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
