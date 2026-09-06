using FluentValidation;

namespace Application.Features.Authentication.Google.SignIn;

/// <summary>Valida limites sintáticos antes da verificação criptográfica.</summary>
public sealed class GoogleSignInCommandValidator : AbstractValidator<GoogleSignInCommand>
{
    public GoogleSignInCommandValidator()
    {
        RuleFor(command => command.IdToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("google_sign_in_id_token_required")
            .MaximumLength(10_000)
            .WithErrorCode("google_sign_in_id_token_too_long");

        RuleFor(command => command.RawNonce)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("google_sign_in_nonce_required")
            .MaximumLength(512)
            .WithErrorCode("google_sign_in_nonce_too_long");

        RuleFor(command => command.InvitationToken)
            .MaximumLength(512)
            .WithErrorCode("google_sign_in_invitation_token_too_long")
            .When(command => command.InvitationToken is not null);
    }
}
