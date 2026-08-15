using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;

/// <summary>Valida a solicitação de atribuição de um tipo de pack a um cliente.</summary>
public sealed class AssignClientSessionPackCommandValidator
    : AbstractValidator<AssignClientSessionPackCommand>
{
    public AssignClientSessionPackCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("client_session_pack_client_id_required");

        RuleFor(command => command.PackTypeId)
            .NotEmpty()
            .WithErrorCode("pack_type_id_required");

        RuleFor(command => command.PurchaseDate)
            .NotEmpty()
            .WithErrorCode("client_session_pack_purchase_date_required");

        RuleFor(command => command.ExpectedEndDate)
            .GreaterThanOrEqualTo(command => command.PurchaseDate)
            .When(command =>
                command.ExpectedEndDate.HasValue
                && command.PurchaseDate != default)
            .WithErrorCode("expected_end_date_before_purchase");
    }
}
