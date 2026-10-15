using FluentValidation;

namespace Application.Features.Authentication.RegisterInvitedClient;

/// <summary>Valida token, limites da password e confirmação.</summary>
public sealed class RegisterInvitedClientCommandValidator
    : AbstractValidator<RegisterInvitedClientCommand>
{
    public RegisterInvitedClientCommandValidator()
    {
        RuleFor(command => command.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_invite_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_invite_token_invalid");

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_password_required")
            .MinimumLength(8)
            .WithErrorCode("authentication_password_too_short")
            .MaximumLength(128)
            .WithErrorCode("authentication_password_too_long");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password)
            .WithErrorCode("authentication_password_confirmation_mismatch")
            .WithMessage("Password confirmation must match the password.");
    }
}
