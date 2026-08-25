using Domain.Exceptions;

namespace Domain.Entities.Identity;

/// <summary>
/// Credencial de utilização única para confirmar o email de uma conta local.
/// Apenas o hash do token é conservado.
/// </summary>
public sealed class EmailVerificationToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    private EmailVerificationToken() { }

    /// <summary>Cria uma credencial de confirmação ainda não consumida.</summary>
    public EmailVerificationToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        DateTime now)
    {
        ValidateFields(userId, expiresAt, now);

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = NormalizeAndValidateTokenHash(tokenHash);
        ExpiresAt = expiresAt;
        CreatedAt = now;
    }

    /// <summary>Indica se a credencial ainda pode ser consumida.</summary>
    public bool CanConsume(DateTime now) =>
        ConsumedAt is null && ExpiresAt > now;

    /// <summary>Regista o consumo único da credencial.</summary>
    public void MarkConsumed(DateTime now)
    {
        if (!CanConsume(now))
            throw new DomainException("The token is not consumable.");

        ConsumedAt = now;
    }

    private static string NormalizeAndValidateTokenHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Token hash is required.");

        var normalizedHash = tokenHash.Trim().ToUpperInvariant();
        if (normalizedHash.Length != 64 ||
            !normalizedHash.All(char.IsAsciiHexDigit))
            throw new DomainException(
                "Token hash must contain exactly 64 hexadecimal characters.");

        return normalizedHash;
    }

    private static void ValidateFields(
        Guid userId,
        DateTime expiresAt,
        DateTime now)
    {
        if (userId == Guid.Empty)
            throw new DomainException("User is required.");
        if (expiresAt <= now)
            throw new DomainException("Expiry date must be after creation date.");
    }
}
