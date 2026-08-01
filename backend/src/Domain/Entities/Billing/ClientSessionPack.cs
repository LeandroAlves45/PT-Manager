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
    public string PackName { get; private set; } = null!;
    public int SessionTotal { get; private set; }
    public int SessionsRemaining { get; private set; }
    public int PriceCents { get; private set; }
    public string Currency { get; private set; } = null!;
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
        PackType packType,
        DateOnly purchaseDate,
        DateOnly? expirationDate,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Owner trainer ID and client ID are required.");
        if (packType.OwnerTrainerId != ownerTrainerId)
            throw new DomainException("Pack type belongs to another personal trainer.");
        if (!packType.IsActive || packType.IsDeleted)
            throw new DomainException("Inactive pack type cannot be assigned.");
        if (expirationDate.HasValue && expirationDate.Value < purchaseDate)
            throw new DomainException("Pack expiration date must be after purchase date.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        PackTypeId = packType.Id;
        PackName = packType.Name;
        SessionTotal = packType.SessionCount;
        SessionsRemaining = packType.SessionCount;
        PriceCents = packType.PriceCents;
        Currency = packType.Currency;
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
            throw new DomainException("Pack has no usable sessions.");

        SessionsRemaining -= 1;
        UpdatedAt = now;
    }

    /// <summary>Repõe uma sessão após correção administrativa validada.</summary>
    public void RestoreSession(DateTime now)
    {
        EnsureNotDeleted();
        if (SessionsRemaining >= SessionTotal)
            throw new DomainException("Pack already contains its full session balance.");

        SessionsRemaining += 1;
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
            throw new DomainException("Cannot modify a deleted client session pack.");
    }
}
