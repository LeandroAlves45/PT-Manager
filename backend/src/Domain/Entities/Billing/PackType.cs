using Domain.Exceptions;
namespace Domain.Entities.Billing;

/// <summary>
/// Tipo de pack de sessões vendida pelo Personal Trainer aos seus clientes
/// (Ex: "10 sessões / 300€ / 90 dias").
/// </summary>
public class PackType
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SessionCount { get; private set; }
    public int PriceCents { get; private set; }
    public string Currency { get; private set; } = null!;
    public int? DurationDays { get; private set; }
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
        int? durationDays,
        DateTime now
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCurrency = NormalizeCurrency(currency);
        ValidateParameters(normalizedName, sessionCount, priceCents, durationDays);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        Name = normalizedName;
        SessionCount = sessionCount;
        PriceCents = priceCents;
        Currency = normalizedCurrency;
        DurationDays = durationDays;
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza um tipo de pack de sessões.</summary>
    public void Update(
        string name,
        int sessionCount,
        int priceCents,
        string currency,
        int? durationDays,
        DateTime now
    )
    {
        EnsureNotDeleted();
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCurrency = NormalizeCurrency(currency);
        ValidateParameters(normalizedName, sessionCount, priceCents, durationDays);

        Name = normalizedName;
        SessionCount = sessionCount;
        PriceCents = priceCents;
        Currency = normalizedCurrency;
        DurationDays = durationDays;
        UpdatedAt = now;
    }

    /// <summary>Impede novas atribuções sem apagar histórico.</summary>
    public void Deactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Volta a disponibilizar o tipo de pack.</summary>
    public void Reactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do tipo de pack de sessões.</summary>
    public void SoftDelete(DateTime now)
    {
        EnsureNotDeleted();
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros de criação/atualização do tipo de pack.</summary>
    private void ValidateParameters(
        string name,
        int sessionCount,
        int priceCents,
        int? durationDays
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Pack type name cannot be empty.");
        if (name.Length > 255)
            throw new DomainException("Pack type name cannot exceed 255 characters.");

        if (sessionCount <= 0)
            throw new DomainException("Session count must be greater than zero.");

        if (priceCents < 0)
            throw new DomainException("Price in cents cannot be negative.");

        if (durationDays.HasValue && durationDays.Value <= 0)
            throw new DomainException("Duration in days must be greater than zero if specified.");
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
