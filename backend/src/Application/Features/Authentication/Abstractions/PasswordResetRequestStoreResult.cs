namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados internos do pedido de reset de password.</summary>
public enum PasswordResetRequestStoreStatus
{
    Issued,
    NotEligible,
    ConcurrencyConflict
}

/// <summary>Resultado persistente da emissão de reset.</summary>
public sealed record PasswordResetRequestStoreResult(
    PasswordResetRequestStoreStatus Kind,
    IssuedAuthenticationSecret? Secret
)
{
    public static PasswordResetRequestStoreResult Issued(
        IssuedAuthenticationSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return new(PasswordResetRequestStoreStatus.Issued, secret);
    }

    public static PasswordResetRequestStoreResult For(
        PasswordResetRequestStoreStatus status)
    {
        if (status == PasswordResetRequestStoreStatus.Issued)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null);
    }
}
