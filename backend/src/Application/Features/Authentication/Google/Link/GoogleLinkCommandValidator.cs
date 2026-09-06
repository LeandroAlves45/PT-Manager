using FluentValidation;

namespace Application.Features.Authentication.Google.Link;

/// <summary>Valida limites sintáticos do linking antes do store transacional.</summary>
public sealed class GoogleLinkCommandValidator : AbstractValidator<GoogleLinkCommand>
{
    public GoogleLinkCommandValidator()
    {
        RuleFor(command => command.IdToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("google_link_id_token_required")
            .MaximumLength(10_000)
            .WithErrorCode("google_link_id_token_too_long");

        RuleFor(command => command.RawNonce)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("google_link_nonce_required")
            .MaximumLength(512)
            .WithErrorCode("google_link_nonce_too_long");

        RuleFor(command => command.CurrentPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("google_link_current_password_required")
            .MaximumLength(255)
            .WithErrorCode("google_link_current_password_too_long");
    }
}
