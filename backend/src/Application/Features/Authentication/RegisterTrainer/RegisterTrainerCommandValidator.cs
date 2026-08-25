using FluentValidation;

namespace Application.Features.Authentication.RegisterTrainer;

/// <summary>Valida os limites sintáticos do signup local.</summary>
public sealed class RegisterTrainerCommandValidator
    : AbstractValidator<RegisterTrainerCommand>
{
    public RegisterTrainerCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_email_required")
            .MaximumLength(255)
            .WithErrorCode("authentication_email_too_long")
            .EmailAddress()
            .WithErrorCode("authentication_email_invalid");

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_password_required")
            .MinimumLength(8)
            .WithErrorCode("authentication_password_too_short")
            .MaximumLength(128)
            .WithErrorCode("authentication_password_too_long");

        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_full_name_required")
            .MaximumLength(255)
            .WithErrorCode("authentication_full_name_too_long");
    }
}
