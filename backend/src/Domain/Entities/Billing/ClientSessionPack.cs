using Domain.Exceptions;

namespace Domain.Entities.Billing;

/// <summary>
/// Pack de sessões comprado por um cliente, saldo de sessões restantes e expiração do pack.
/// O débito da sessão acontece quando a sessão é concluída, não quando é agendada.
/// </summary>
public class ClientSessionPack
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid PackTypeId { get; private set; }
    public int SessionsRemaining { get; private set; }
    public DateOnly PurchaseDate { get; private set; }
    public DateOnly? ExpirationDate { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ClientSessionPack() { }

    /// <summary>
    /// Cria um pack comprado, copiando o saldo inicial do tipo de pack e
    /// calcula a expiração a partir da duração do tipo de pack.
    /// </summary>
    public ClientSessionPack(
        Guid ownerTrainerId,
        Guid clientId,
        Guid packTypeId,
        int initialSessions,
        DateOnly purchaseDate,
        DateOnly? expirationDate,
        DateTime now
    )
    {
        if (initialSessions <= 0)
            throw new DomainException("Pack must start with available sessions.");
        if (expirationDate.HasValue && expirationDate.Value < purchaseDate)
            throw new DomainException("Pack expiration date must be after purchase date.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        PackTypeId = packTypeId;
        SessionsRemaining = initialSessions;
        PurchaseDate = purchaseDate;
        ExpirationDate = expirationDate;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>True se o pack ainda tem saldo e não expirou.</summary>
    public bool IsUsable(DateOnly today)
        => !IsDeleted &&
            SessionsRemaining > 0 &&
            (!ExpirationDate.HasValue || ExpirationDate.Value >= today);

    /// <summary>Debita uma sessão do pack, se ainda houver saldo.</summary>
    public void ConsumeSession(DateOnly today, DateTime now)
    {
        EnsureNotDeleted();
        if (!IsUsable(today))
            throw new DomainException("Pack without remaining sessions or expired cannot be used.");

        SessionsRemaining -= 1;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do pack de sessões.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot consume sessions from a deleted pack.");
    }
}
