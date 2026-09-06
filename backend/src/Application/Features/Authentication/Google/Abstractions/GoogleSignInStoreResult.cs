using Application.Features.Authentication.Abstractions;

namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Invariantes do outcome devolvido pelo store de Google Sign-In.</summary>
public sealed record GoogleSignInStoreResult
{
    public GoogleSignInStoreStatus Kind { get; }
    public AuthenticatedPrincipal? Principal { get; }
    public IssuedRefreshSession? RefreshSession { get; }
    public IssuedAuthenticationSecret? EmailConfirmation { get; }

    private GoogleSignInStoreResult(
        GoogleSignInStoreStatus kind,
        AuthenticatedPrincipal? principal,
        IssuedRefreshSession? refreshSession,
        IssuedAuthenticationSecret? emailConfirmation)
    {
        Kind = kind;
        Principal = principal;
        RefreshSession = refreshSession;
        EmailConfirmation = emailConfirmation;
    }

    public static GoogleSignInStoreResult Authenticated(
        AuthenticatedPrincipal principal,
        IssuedRefreshSession session) =>
        new(GoogleSignInStoreStatus.Authenticated,
            principal ?? throw new ArgumentNullException(nameof(principal)),
            session ?? throw new ArgumentNullException(nameof(session)),
            null);

    public static GoogleSignInStoreResult ConfirmationRequired(
        IssuedAuthenticationSecret confirmation) =>
        new(GoogleSignInStoreStatus.EmailConfirmationRequired,
            null,
            null,
            confirmation ?? throw new ArgumentNullException(nameof(confirmation)));

    public static GoogleSignInStoreResult Failure(GoogleSignInStoreStatus status)
    {
        if (status is GoogleSignInStoreStatus.Authenticated or
            GoogleSignInStoreStatus.EmailConfirmationRequired)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null, null, null);
    }
}
