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

/// <summary>Resultado persistente da confirmação de email.</summary>
public sealed record EmailConfirmationStoreResult(
    EmailConfirmationStoreStatus Kind,
    IssuedAuthenticationSecret? Secret
)
{
    public static EmailConfirmationStoreResult Issued(
        IssuedAuthenticationSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return new(EmailConfirmationStoreStatus.Issued, secret);
    }

    public static EmailConfirmationStoreResult For(
        EmailConfirmationStoreStatus status)
    {
        if (status == EmailConfirmationStoreStatus.Issued)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null);
    }
}
