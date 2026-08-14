using Domain.Exceptions;
namespace Domain.Entities.Billing;

/// <summary>
/// Define um tipo de pack comercializado por um personal trainer.
/// Alterações ao catálogo não reescrevem snapshots já atribuídos a clientes.
/// </summary>
public sealed class PackType
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SessionCount { get; private set; }
    public int PriceCents { get; private set; }
    public string Currency { get; private set; } = null!;
    public int? ExpectedDurationDays { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PackType() { }

    /// <summary>Cria um tipo de pack de sessões.</summary>
    public PackType(
        Guid ownerTrainerId,
        string name,
        int sessionCount,
        int priceCents,
        string currency,
        int? expectedDurationDays,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty)
            throw new DomainException("Owner trainer ID is required.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ApplyFields(
            name,
            sessionCount,
            priceCents,
            currency,
            expectedDurationDays
        );
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza a oferta futura sem alterar ClientSessionPacks existentes.</summary>
    public void Update(
        string name,
        int sessionCount,
        int priceCents,
        string currency,
        int? expectedDurationDays,
        DateTime now
    )
    {
        EnsureNotDeleted();
        ApplyFields(
            name,
            sessionCount,
            priceCents,
            currency,
            expectedDurationDays
        );
        UpdatedAt = now;
    }

    /// <summary>Impede novas atribuições e preserva snapshots existentes.</summary>
    public void Archive(DateTime now)
    {
        EnsureNotDeleted();
        if (!IsActive)
            return;
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Volta a disponibilizar o tipo para novas atribuições.</summary>
    public void Reactivate(DateTime now)
    {
        EnsureNotDeleted();
        if (IsActive)
            return;
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do tipo de pack de sessões.</summary>
    public void SoftDelete(DateTime now)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }

    private void ApplyFields(
        string name,
        int sessionCount,
        int priceCents,
        string currency,
        int? expectedDurationDays
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCurrency = NormalizeCurrency(currency);

        if (normalizedName.Length is 0 or > 255)
            throw new DomainException(
                "Pack type name must contain between 1 and 255 characters."
            );
        if (sessionCount <= 0)
            throw new DomainException("Session count must be greater than zero.");
        if (priceCents < 0)
            throw new DomainException("Price in cents cannot be negative.");
        if (expectedDurationDays.HasValue && expectedDurationDays.Value <= 0)
            throw new DomainException(
                "Expected duration must be greater than zero when specified."
            );

        Name = normalizedName;
        SessionCount = sessionCount;
        PriceCents = priceCents;
        Currency = normalizedCurrency;
        ExpectedDurationDays = expectedDurationDays;
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || !normalized.All(char.IsLetter))
            throw new DomainException("Currency must be a three-letter ISO code.");
        return normalized;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted pack type.");
    }
}
