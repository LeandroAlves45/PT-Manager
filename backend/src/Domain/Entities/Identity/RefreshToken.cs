using Domain.Exceptions;
namespace Domain.Entities.Identity;

/// <summary>
/// Refresh token persistido (apenas o hash) para permitir refresh de access token sem re-login.
/// Cada refresh cria um novo token da mesma família e revoga o anterior.
/// </summary>
/// <remarks>
/// Deteção de reuso: se chegar um token com <see cref="RevokedAt"/> preenchido,
/// significa que um token já rodado está a ser reutilizado — provável roubo.
/// </remarks>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>Agrupa toda a cadeia de rotação de uma sessão de login.</summary>
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string CsrfTokenHash { get; private set; } = null!;
    public Guid? RotatedFromId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefreshToken() { } // EF Core

    /// <summary>
    /// Criar o primeiro token de uma nova família (login) ou um token rodado (refresh).
    /// </summary>
    public RefreshToken(
        Guid userId,
        Guid familyId,
        string tokenHash,
        string csrfTokenHash,
        Guid? rotatedFromId,
        DateTime expiresAt,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Refresh token hash is required");
        if (tokenHash.Length > 255)
            throw new DomainException("Refresh token hash cannot exceed 255 characters");
        if (expiresAt <= now)
            throw new DomainException("Refresh token expiration must be in the future");

        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CsrfTokenHash = NormalizeAndValidateCsrfToken(csrfTokenHash);
        RotatedFromId = rotatedFromId;
        ExpiresAt = expiresAt;
        RevokedAt = null;
        CreatedAt = now;
    }

    /// <summary>True se o token pode ser usado: não revogado e não expirado.</summary>
    public bool IsActive(DateTime now) => RevokedAt == null && ExpiresAt > now;

    /// <summary>
    /// True se este token já foi rodado/revogado -> a sua reutilização indica
    /// compromisso e deve revogar a família inteira.
    /// </summary>
    public bool IsReused() => RevokedAt != null;

    /// <summary>Revoga o token (rotação normal, logout ou revogação de uma família).</summary>
    public void Revoke(DateTime now)
    {
        // Idempotente: revogar duas vezes não altera nada
        if (RevokedAt == null)
            RevokedAt = now;
    }

    /// <summary>Substitui o segredo anti-CSRF mantendo a mesma sessão de refresh.</summary>
    public void ReplaceCsrfTokenHash(string csrfTokenHash, DateTime now)
    {
        if (!IsActive(now))
            throw new DomainException("An inactive refresh token cannot rotate its CSRF secret");

        CsrfTokenHash = NormalizeAndValidateCsrfToken(csrfTokenHash);
    }

    private static string NormalizeAndValidateCsrfToken(string csrfTokenHash)
    {
        if (string.IsNullOrWhiteSpace(csrfTokenHash))
            throw new DomainException("CSRF token hash is required");

        var normalizedHash = csrfTokenHash.Trim().ToUpperInvariant();
        if (normalizedHash.Length != 64 ||
            !normalizedHash.All(char.IsAsciiHexDigit))
            throw new DomainException(
                "CSRF token hash must contain exactly 64 hexadecimal characters"
            );

        return normalizedHash;
    }
}
