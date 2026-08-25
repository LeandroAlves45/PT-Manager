namespace Application.Features.Authentication.Abstractions;

/// <summary>Refresh token acabado de persistir; o valor bruto nunca é guardado.</summary>
public sealed record IssuedRefreshSession
{
    public string RawToken { get; }
    public DateTime ExpiresAt { get; }

    /// <summary>Impede que um store devolva a sessão incompleta.</summary>
    public IssuedRefreshSession(string rawToken, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ArgumentException("Raw refresh token is required.", nameof(rawToken));

        RawToken = rawToken;
        ExpiresAt = expiresAt;
    }
}
