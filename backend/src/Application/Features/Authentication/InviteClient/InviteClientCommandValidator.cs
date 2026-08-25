using FluentValidation;

namespace Application.Features.Authentication.InviteClient;

/// <summary>Valida o pedido de convite de cliente.</summary>
public sealed class InviteClientCommandValidator : AbstractValidator<InviteClientCommand>
{
    public InviteClientCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("authentication_client_id_required");

        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_email_required")
            .MaximumLength(255)
            .WithErrorCode("authentication_email_too_long")
            .EmailAddress()
            .WithErrorCode("authentication_email_invalid");
    }
}
