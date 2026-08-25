namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados da emissão do convite.</summary>
public enum IssueClientInvitationStoreStatus
{
    Issued,
    ClientNotFound,
    ClientInactive,
    EmailMismatch,
    RelationshipConflict,
    ConcurrencyConflict
}

/// <summary>Resultado da emissão persistida de um convite.</summary>
public sealed record IssueClientInvitationStoreResult(
    IssueClientInvitationStoreStatus Kind,
    IssuedAuthenticationSecret? Secret
)
{
    public static IssueClientInvitationStoreResult Issued(
        IssuedAuthenticationSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return new(IssueClientInvitationStoreStatus.Issued, secret);
    }

    public static IssueClientInvitationStoreResult For(
        IssueClientInvitationStoreStatus status)
    {
        if (status == IssueClientInvitationStoreStatus.Issued)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null);
    }
}
