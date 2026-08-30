using FluentValidation;

namespace Application.Features.Authentication.RefreshSession;

/// <summary>Valida presença e tamanho do refresh token.</summary>
public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(command => command.RawToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_refresh_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_refresh_token_invalid")
            .WithMessage("Refresh token is invalid.");

        RuleFor(command => command.RawCsrfToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_csrf_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_csrf_token_invalid")
            .WithMessage("Anti-CSRF token is invalid.");
    }
}
