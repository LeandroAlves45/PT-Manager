namespace Application.Features.Authentication.Abstractions;

/// <summary>Access token emitido para um principal autenticado.</summary>
public sealed record IssuedAccessToken
{
    public string Token { get; }
    public DateTime ExpiresAt { get; }

    /// <summary>Impede outputs incompletos do emissor.</summary>
    public IssuedAccessToken(string token, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Access token is required.", nameof(token));

        Token = token;
        ExpiresAt = expiresAt;
    }
}
