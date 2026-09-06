using Domain.Exceptions;

namespace Domain.Entities.Identity;

/// <summary>Challenge efémero que impede replay entre sign-in e linking.</summary>
public sealed class ExternalAuthenticationChallenge
{
    public const string SignInPurpose = "sign_in";
    public const string LinkPurpose = "link";

    public Guid Id { get; private set; }
    public string NonceHash { get; private set; } = null!;
    public string Purpose { get; private set; } = null!;
    public Guid? UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private ExternalAuthenticationChallenge() { }

    public ExternalAuthenticationChallenge(
        string nonceHash,
        string purpose,
        Guid? userId,
        DateTime expiresAt,
        DateTime now)
    {
        var normalizedHash = nonceHash?.Trim().ToUpperInvariant();
        if (normalizedHash is null || normalizedHash.Length != 64 ||
            !normalizedHash.All(char.IsAsciiHexDigit))
            throw new DomainException("External challenge nonce hash must be SHA-256 hexadecimal");

        if (purpose is not (SignInPurpose or LinkPurpose))
            throw new DomainException("External challenge purpose is invalid");
        if (purpose == SignInPurpose && userId.HasValue || purpose == LinkPurpose && !userId.HasValue)
            throw new DomainException("External challenge actor does not match its purpose");

        if (userId == Guid.Empty)
            throw new DomainException("External challenge user cannot be empty");
        if (now.Kind != DateTimeKind.Utc || expiresAt.Kind != DateTimeKind.Utc || expiresAt <= now)
            throw new DomainException("External challenge expiration must be future UTC");

        Id = Guid.NewGuid();
        NonceHash = normalizedHash;
        Purpose = purpose;
        UserId = userId;
        CreatedAt = now;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;
}
