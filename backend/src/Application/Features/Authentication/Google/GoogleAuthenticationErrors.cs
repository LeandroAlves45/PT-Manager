using Application.Errors;

namespace Application.Features.Authentication.Google;

/// <summary>Códigos estáveis e seguros dos casos de uso Google Sign-In e linking.</summary>
public static class GoogleAuthenticationErrors
{
    public static readonly Error InvalidCredential = Error.Create(
        "google_credential_invalid",
        ErrorCategory.Unauthorized,
        "The Google credential or challenge is invalid or expired.");

    public static readonly Error ProviderUnavailable = Error.Create(
        "google_provider_unavailable",
        ErrorCategory.ExternalDependency,
        "Google authentication is temporarily unavailable.");

    public static readonly Error AccountLinkRequired = Error.Create(
        "google_account_link_required",
        ErrorCategory.Conflict,
        "An account with this email already exists and must be linked explicitly.");

    public static readonly Error EmailNotVerified = Error.Create(
        "google_email_not_verified",
        ErrorCategory.Forbidden,
        "Google has not verified the supplied email.");

    public static readonly Error LinkingEmailMismatch = Error.Create(
        "google_link_email_mismatch",
        ErrorCategory.Conflict,
        "The Google email must match the PT Manager account email.");

    public static readonly Error IdentityConflict = Error.Create(
        "google_identity_conflict",
        ErrorCategory.Conflict,
        "The Google identity is already associated or changed concurrently.");
}
