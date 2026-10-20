using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Identity;

/// <summary>
/// Convite de uso único do superuser para um email que ainda não tem conta (RN-008, FL-005).
/// Quem aceita cria a conta de treinador com palavra-passe e nasce no Pro isento.
/// </summary>
public sealed class TrainerInvitation
{
    /// <summary>Validade contada desde o envio.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private TrainerInvitation() { } // EF Core

    /// <summary>Cria um convite pendente válido por <see cref="Lifetime"/>.</summary>
    public TrainerInvitation(
        EmailAddress email,
        string tokenHash,
        Guid createdByUserId,
        DateTime now
    )
    {
        ArgumentNullException.ThrowIfNull(email);
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length > 255)
            throw new DomainException("Invite token hash must contain between 1 and 255 characters");
        if (createdByUserId == Guid.Empty)
            throw new DomainException("Invitation creator is required.");

        Id = Guid.NewGuid();
        Email = email.Value;
        NormalizedEmail = email.Normalized;
        TokenHash = tokenHash;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        ExpiresAt = now.Add(Lifetime);
    }

    public bool IsUsed => UsedAt.HasValue;
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>Pendente: nem usado nem anulado. Pode já ter expirado.</summary>
    public bool IsPending => !IsUsed && !IsRevoked;

    /// <summary>True se ainda pode ser aceite.</summary>
    public bool IsValid(DateTime now) => IsPending && ExpiresAt > now;

    /// <summary>Marca o convite como usado; a concorrência é garantida pela persistência.</summary>
    public void MarkUsed(DateTime now)
    {
        if (!IsValid(now))
            throw new DomainException("Invite invalid, used or expired");
        UsedAt = now;
    }

    /// <summary>Anula um convite pendente. Repetir não altera a data original.</summary>
    public bool Revoke(DateTime now)
    {
        if (!IsPending)
            return false;
        RevokedAt = now;
        return true;
    }
}
