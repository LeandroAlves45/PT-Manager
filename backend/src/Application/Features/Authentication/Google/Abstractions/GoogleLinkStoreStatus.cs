namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Resultados fechados da transação de linking Google.</summary>
public enum GoogleLinkStoreStatus
{
    Linked,
    ChallengeInvalid,
    UserNotFound,
    PasswordInvalid,
    EmailMismatch,
    IdentityConflict,
    ConcurrencyConflict
}
