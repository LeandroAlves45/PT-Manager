namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Resultados fechados da transação de Google Sign-In.</summary>
public enum GoogleSignInStoreStatus
{
    Authenticated,
    EmailConfirmationRequired,
    ChallengeInvalid,
    AccountLinkRequired,
    AccountInactive,
    RelationshipInactive,
    InvitationInvalid,
    InvitationExpired,
    InvitationConsumed,
    InvitationEmailMismatch,
    RelationshipConflict,
    ConcurrencyConflict
}
