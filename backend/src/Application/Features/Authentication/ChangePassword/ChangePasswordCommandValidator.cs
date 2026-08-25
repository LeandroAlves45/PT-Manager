using FluentValidation;

namespace Application.Features.Authentication.ChangePassword;

/// <summary>Valida presença, limites e confirmação da nova password.</summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_current_password_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_current_password_invalid")
            .WithMessage("Current password is invalid.");

        RuleFor(command => command.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_new_password_required")
            .MinimumLength(8)
            .WithErrorCode("authentication_new_password_too_short")
            .MaximumLength(128)
            .WithErrorCode("authentication_new_password_too_long")
            .WithMessage("New password is invalid.");

        RuleFor(command => command.ConfirmNewPassword)
            .Cascade(CascadeMode.Stop)
            .Equal(command => command.NewPassword)
            .WithErrorCode("authentication_password_confirmation_mismatch")
            .WithMessage("Password confirmation must match the new password.");
    }
}
