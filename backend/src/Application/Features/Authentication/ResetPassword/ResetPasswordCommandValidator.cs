using FluentValidation;

namespace Application.Features.Authentication.ResetPassword;

/// <summary>Valida token, limites e confirmação da nova credencial.</summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_password_reset_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_password_reset_token_invalid")
            .WithMessage("Password reset token is invalid.");

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
