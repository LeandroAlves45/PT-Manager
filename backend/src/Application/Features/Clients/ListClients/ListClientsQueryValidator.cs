using Application.Validation;
using FluentValidation;

namespace Application.Features.Clients.ListClients;

/// <summary>Valida pesquisa, filtro e paginação da listagem.</summary>
public sealed class ListClientsQueryValidator : AbstractValidator<ListClientsQuery>
{
    /// <summary>Inicializa as regras canónicas da query.</summary>
    public ListClientsQueryValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(255)
            .When(query => !string.IsNullOrWhiteSpace(query.Search))
            .WithErrorCode("client_search_too_long")
            .WithMessage("Search cannot exceed 255 characters.");

        RuleFor(query => query.Activity)
            .IsInEnum()
            .WithErrorCode("client_activity_filter_invalid")
            .WithMessage("Activity filter must be Active, Archived or All.");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
