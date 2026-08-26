namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados persistentes possíveis nas operações de password.</summary>
public enum PasswordManagementStoreStatus
{
    Changed,
    UserNotFound,
    CurrentPasswordInvalid,
    NewPasswordInvalid,
    ResetTokenNotFound,
    ResetTokenExpired,
    ResetTokenConsumed,
    ConcurrencyConflict
}
