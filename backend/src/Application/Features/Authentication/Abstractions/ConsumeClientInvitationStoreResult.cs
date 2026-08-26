namespace Application.Features.Authentication.Abstractions;

/// <summary>Resultado do consumo atómico de um convite.</summary>
public sealed record ConsumeClientInvitationStoreResult(
    ConsumeClientInvitationStoreStatus Kind,
    AuthenticatedPrincipal? Principal,
    IssuedRefreshSession? RefreshSession
)
{
    public static ConsumeClientInvitationStoreResult Accepted(
        AuthenticatedPrincipal principal,
        IssuedRefreshSession refreshSession)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(refreshSession);

        return new(
            ConsumeClientInvitationStoreStatus.Accepted,
            principal,
            refreshSession
        );
    }

    public static ConsumeClientInvitationStoreResult For(
        ConsumeClientInvitationStoreStatus status)
    {
        if (status == ConsumeClientInvitationStoreStatus.Accepted)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null, null);
    }
}
