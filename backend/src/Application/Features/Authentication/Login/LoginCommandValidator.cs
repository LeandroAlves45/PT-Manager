using FluentValidation;

namespace Application.Features.Authentication.Login;

/// <summary>Valida limites defensivos das credenciais de login local.</summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_email_required")
            .MaximumLength(255)
            .WithErrorCode("authentication_email_too_long")
            .EmailAddress()
            .WithErrorCode("authentication_email_invalid")
            .WithMessage("Email is invalid.");

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_password_required")
            .MinimumLength(8)
            .WithErrorCode("authentication_password_too_short")
            .MaximumLength(128)
            .WithErrorCode("authentication_password_too_long")
            .WithMessage("Password is invalid.");
    }
}
