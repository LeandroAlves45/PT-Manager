using Domain.ValueObjects;
using Domain.Exceptions;

namespace Domain.Entities.Identity;

/// <summary>
/// Convite enviado por um Personal Trainer a um futuro cliente. Uso único, com expiração.
/// Só o hash do token é persistido.
/// </summary>
public class InviteToken
{
    public Guid Id { get; private set; }
    /// <summary>Personal trainer que enviou o convite.</summary>
    public Guid TrainerId { get; private set; }
    public string Email { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private InviteToken() { } // EF Core

    /// <summary>Cria um convite válido por um período definido.</summary>
    public InviteToken(
        Guid trainerId,
        EmailAddress email,
        string tokenHash,
        DateTime expiresAt,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Invite token hash is required");
        if (tokenHash.Length > 255)
            throw new DomainException("Invite token hash cannot exceed 255 characters");
        if (expiresAt <= now)
            throw new DomainException("Invite token expiration must be in the future.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        Email = email.Value;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        IsUsed = false;
        CreatedAt = now;
    }

    /// <summary>True se o convite ainda pode ser aceite.</summary>
    public bool IsValid(DateTime now) => !IsUsed && ExpiresAt > now;

    /// <summary>
    /// Marca o convite como usado no modelo em memória.
    /// A garantia contra duas aceitações simultâneas pertence à operação
    /// condicional/concorrência da persistência.
    /// </summary>
    public void MarkUsed(DateTime now)
    {
        if (!IsValid(now))
            throw new DomainException("Invite invalid, used or expired");
        IsUsed = true;
    }
}
