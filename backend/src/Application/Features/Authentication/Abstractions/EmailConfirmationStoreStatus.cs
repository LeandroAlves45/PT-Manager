namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados da emissão e consumo de confirmação.</summary>
public enum EmailConfirmationStoreStatus
{
    Issued,
    Confirmed,
    UserNotFound,
    AlreadyConfirmed,
    AccountInactive,
    TokenNotFound,
    TokenExpired,
    TokenAlreadyConsumed,
    ConcurrencyConflict
}
