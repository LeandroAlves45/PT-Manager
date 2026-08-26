namespace Application.Features.Authentication.Abstractions;

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
