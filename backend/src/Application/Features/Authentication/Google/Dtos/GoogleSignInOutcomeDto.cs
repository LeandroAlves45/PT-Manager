using Application.Features.Authentication.Dtos;

namespace Application.Features.Authentication.Google.Dtos;

/// <summary>Representa sessão autenticada ou confirmação de email pendente.</summary>
public sealed record GoogleSignInOutcomeDto
{
    public AuthenticationSessionDto? Session { get; }
    public bool IsEmailConfirmationRequired { get; }

    private GoogleSignInOutcomeDto(
        AuthenticationSessionDto? session,
        bool isEmailConfirmationRequired)
    {
        Session = session;
        IsEmailConfirmationRequired = isEmailConfirmationRequired;
    }

    public static GoogleSignInOutcomeDto Authenticated(AuthenticationSessionDto session) =>
        new(session ?? throw new ArgumentNullException(nameof(session)), false);

    public static GoogleSignInOutcomeDto ConfirmationRequired() => new(null, true);
}
