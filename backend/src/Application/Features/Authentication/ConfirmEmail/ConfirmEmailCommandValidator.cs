using FluentValidation;

namespace Application.Features.Authentication.ConfirmEmail;

/// <summary>Valida o contrato da confirmação de email.</summary>
public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_confirmation_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_confirmation_token_invalid");
    }
}
