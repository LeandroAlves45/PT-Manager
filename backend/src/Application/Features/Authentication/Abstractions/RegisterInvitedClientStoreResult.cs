namespace Application.Features.Authentication.Abstractions;

/// <summary>Resultado do registo atómico de um cliente convidado.</summary>
public sealed record RegisterInvitedClientStoreResult(
    RegisterInvitedClientStoreStatus Kind,
    AuthenticatedPrincipal? Principal,
    IssuedRefreshSession? RefreshSession)
{
    /// <summary>Registo concluído: conta criada, ficha ligada, convite consumido e sessão aberta.</summary>
    public static RegisterInvitedClientStoreResult Registered(
        AuthenticatedPrincipal principal,
        IssuedRefreshSession refreshSession)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(refreshSession);

        return new(RegisterInvitedClientStoreStatus.Registered, principal, refreshSession);
    }

    /// <summary>Resultado sem sessão para qualquer estado diferente de <c>Registered</c>.</summary>
    public static RegisterInvitedClientStoreResult For(
        RegisterInvitedClientStoreStatus status)
    {
        if (status == RegisterInvitedClientStoreStatus.Registered)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null, null);
    }
}
