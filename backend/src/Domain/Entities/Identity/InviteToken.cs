using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Identity;

/// <summary>Convite de uso único que liga uma futura conta a uma ficha de cliente concreta.</summary>
public sealed class InviteToken
{
    public Guid Id { get; private set; }
    /// <summary>Personal trainer que enviou o convite.</summary>
    public Guid TrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Email { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsUsed => UsedAt.HasValue;

    private InviteToken() { } // EF Core

    /// <summary>Cria um convite válido por um período definido.</summary>
    public InviteToken(
        Guid trainerId,
        Guid clientId,
        EmailAddress email,
        string tokenHash,
        DateTime expiresAt,
        DateTime now
    )
    {
        if (trainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Trainer and client IDs are required");
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length > 255)
            throw new DomainException("Invite token hash must contain between 1 and 255 characters");
        if (expiresAt <= now)
            throw new DomainException("Invite token expiration must be in the future.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        ClientId = clientId;
        Email = email.Value;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        UsedAt = null;
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
        UsedAt = now;
    }
}
