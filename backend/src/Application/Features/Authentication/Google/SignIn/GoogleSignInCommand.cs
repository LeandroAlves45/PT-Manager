namespace Application.Features.Authentication.Google.SignIn;

/// <summary>Credencial Google, nonce e convite opcional para sign-in.</summary>
public sealed record GoogleSignInCommand(
    string IdToken,
    string RawNonce,
    string? InvitationToken);
