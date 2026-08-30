using FluentValidation;

namespace Application.Features.Authentication.BootstrapCsrf;

/// <summary>Valida presença e tamanho do refresh token.</summary>
public sealed class BootstrapCsrfCommandValidator : AbstractValidator<BootstrapCsrfCommand>
{
    public BootstrapCsrfCommandValidator()
    {
        RuleFor(command => command.RawToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_refresh_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_refresh_token_invalid")
            .WithMessage("Refresh token is invalid.");
    }
}
