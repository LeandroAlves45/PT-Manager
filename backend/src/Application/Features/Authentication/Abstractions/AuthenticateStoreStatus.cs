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
