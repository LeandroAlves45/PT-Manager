namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Orquestra transações atómicas de Google Sign-In e linking.</summary>
public interface IExternalAuthenticationStore
{
    Task<GoogleSignInStoreResult> SignInAsync(
        VerifiedExternalIdentity identity,
        string rawNonce,
        string? rawInvitationToken,
        DateTime trialEndsAt,
        DateTime confirmationExpiresAt,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken);

    Task<GoogleLinkStoreStatus> LinkAsync(
        Guid userId,
        VerifiedExternalIdentity identity,
        string rawNonce,
        string currentPassword,
        DateTime now,
        CancellationToken cancellationToken);
}
