using Domain.Exceptions;

namespace Domain.Entities.Billing;

/// <summary>
/// Representa um pack atribuído a um cliente, incluindo snapshot comercial e
/// saldo consumido exclusivamente por sessões concluídas ou faltas.
/// </summary>
public sealed class ClientSessionPack
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid PackTypeId { get; private set; }
    public string PackName { get; private set; } = null!;
    public int SessionsTotal { get; private set; }
    public int SessionsRemaining { get; private set; }
    public int PriceCents { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateOnly PurchaseDate { get; private set; }
    public DateOnly? ExpectedEndDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Indica se o saldo dos packs chegou a zero.</summary>
    public bool IsCompleted => SessionsRemaining == 0;

    /// <summary>Indica se o pack pode ser escolhido. A data esperada não participa.</summary>
    public bool IsUsable => !IsDeleted && SessionsRemaining > 0;

    private ClientSessionPack() { }

    /// <summary>Cria um snapshot a partir de um tipo de pack ativo.</summary>
    public ClientSessionPack(
        Guid ownerTrainerId,
        Guid clientId,
        PackType packType,
        DateOnly purchaseDate,
        DateOnly? expectedEndDate,
        DateTime now
    )
    {
        ArgumentNullException.ThrowIfNull(packType);
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Owner trainer ID and client ID are required.");
        if (packType.OwnerTrainerId != ownerTrainerId)
            throw new DomainException("Pack type belongs to another personal trainer.");
        if (!packType.IsActive || packType.IsDeleted)
            throw new DomainException("Inactive pack type cannot be assigned.");

        ValidateExpectedEndDate(purchaseDate, expectedEndDate);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        PackTypeId = packType.Id;
        PackName = packType.Name;
        SessionsTotal = packType.SessionCount;
        SessionsRemaining = packType.SessionCount;
        PriceCents = packType.PriceCents;
        Currency = packType.Currency;
        PurchaseDate = purchaseDate;
        ExpectedEndDate = expectedEndDate;
        CompletedAt = null;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza apenas a expectativa temporal do acordo.</summary>
    public void ChangeExpectedEndDate(DateOnly? expectedEndDate, DateTime now)
    {
        EnsureNotDeleted();
        ValidateExpectedEndDate(PurchaseDate, expectedEndDate);
        if (ExpectedEndDate == expectedEndDate)
            return;

        ExpectedEndDate = expectedEndDate;
        UpdatedAt = now;
    }

    /// <summary>Debita uma sessão do pack, se ainda houver saldo.</summary>
    public void ConsumeSession(DateOnly today, DateTime now)
    {
        EnsureNotDeleted();
        if (!IsUsable)
            throw new DomainException("Pack has no usable sessions.");

        SessionsRemaining -= 1;
        CompletedAt = SessionsRemaining == 0 ? now : null;
        UpdatedAt = now;
    }

    /// <summary>Repõe uma unidade após RestoreSession validado.</summary>
    public void RestoreSession(DateTime now)
    {
        EnsureNotDeleted();
        if (SessionsRemaining >= SessionsTotal)
            throw new DomainException("Pack already contains its full session balance.");

        SessionsRemaining += 1;
        CompletedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Cancela uma atribuição ainda intocada. A ausência de referência a Session
    /// é validada transaciolnamente pelo store, porque não pertence ao aggregado.
    /// </summary>
    public void Cancel(DateTime now)
    {
        if (IsDeleted)
            return;
        if (SessionsRemaining != SessionsTotal)
            throw new DomainException("A used client session pack cannot be cancelled.");

        IsDeleted = true;
        UpdatedAt = now;
    }

    private static void ValidateExpectedEndDate(
        DateOnly purchaseDate,
        DateOnly? expectedEndDate
    )
    {
        if (expectedEndDate.HasValue && expectedEndDate.Value < purchaseDate)
            throw new DomainException(
                "Expected end date cannot be before purchase date."
            );
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted client session pack.");
    }
}
