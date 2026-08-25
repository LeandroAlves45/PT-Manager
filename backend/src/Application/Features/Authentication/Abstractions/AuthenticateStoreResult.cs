namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados persistentes possíveis do login local.</summary>
public enum AuthenticateStoreStatus
{
    Authenticated,
    InvalidCredentials,
    LockedOut,
    EmailNotConfirmed,
    AccountInactive,
    RelationshipInactive,
    ConcurrencyConflict
}

/// <summary>Resultado do login sem transportar entidades.</summary>
public sealed record AuthenticateStoreResult(
    AuthenticateStoreStatus Kind,
    AuthenticatedPrincipal? Principal,
    IssuedRefreshSession? RefreshSession
)
{
    public static AuthenticateStoreResult Authenticated(
        AuthenticatedPrincipal principal,
        IssuedRefreshSession refreshSession)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(refreshSession);

        return new(
            AuthenticateStoreStatus.Authenticated,
            principal,
            refreshSession
        );
    }

    public static AuthenticateStoreResult Failure(
        AuthenticateStoreStatus status)
    {
        if (status == AuthenticateStoreStatus.Authenticated)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null, null);
    }
}
