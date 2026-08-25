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

/// <summary>Resultado sanitizado sem mensagens internas do Identity.</summary>
public sealed record PasswordManagementStoreResult(PasswordManagementStoreStatus Kind)
{
    public static PasswordManagementStoreResult Changed() =>
        new(PasswordManagementStoreStatus.Changed);

    public static PasswordManagementStoreResult Failure(
        PasswordManagementStoreStatus kind)
    {
        if (kind == PasswordManagementStoreStatus.Changed)
            throw new ArgumentOutOfRangeException(nameof(kind));

        return new(kind);
    }
}
