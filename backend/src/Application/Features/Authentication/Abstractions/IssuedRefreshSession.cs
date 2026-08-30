namespace Application.Features.Authentication.Abstractions;

/// <summary>Refresh token acabado de persistir; o valor bruto nunca é guardado.</summary>
public sealed record IssuedRefreshSession
{
    public string RawToken { get; }
    public string RawCsrfToken { get; }
    public DateTime ExpiresAt { get; }

    /// <summary>Impede que um store devolva a sessão incompleta.</summary>
    public IssuedRefreshSession(
        string rawToken,
        string rawCsrfToken,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ArgumentException("Raw refresh token is required.", nameof(rawToken));

        if (string.IsNullOrWhiteSpace(rawCsrfToken))
            throw new ArgumentException("Raw CSRF token is required.", nameof(rawCsrfToken));

        RawToken = rawToken;
        RawCsrfToken = rawCsrfToken;
        ExpiresAt = expiresAt;
    }
}
