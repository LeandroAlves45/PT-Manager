using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;

/// <summary>Valida a consulta de listagem de packs atribuídos.</summary>
public sealed class ListClientSessionPacksQueryValidator
    : AbstractValidator<ListClientSessionPacksQuery>
{
    public ListClientSessionPacksQueryValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEqual(Guid.Empty)
            .When(command => command.ClientId.HasValue)
            .WithErrorCode("client_session_pack_client_id_invalid");

        RuleFor(command => command.Activity)
            .IsInEnum()
            .WithErrorCode("client_session_pack_activity_invalid");

        this.ApplyPaginationRules(
            query => query.PageNumber,
            query => query.PageSize
        );
    }
}
