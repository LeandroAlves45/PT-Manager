using Domain.Exceptions;

namespace Domain.Entities.Supplements;

/// <summary>
/// Suplemento do catálogo, global ou criado pelo personal trainer.
/// </summary>
public class Supplement
{
    public Guid Id { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Unidade de medida: "grams", "ml", "capsules", etc.</summary>
    public string? UnitOfMeasure { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Supplement() { }

    /// <summary>Cria um suplemento de catálogo.</summary>
    public Supplement(
        Guid? createdByUserId,
        string name,
        string? description,
        string? unitOfMeasure,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Supplement name cannot be empty.");

        var normalizedName = name.Trim();
        var normalizedUnitOfMeasure = unitOfMeasure?.Trim();
        ValidateParameters(normalizedName, normalizedUnitOfMeasure);

        Id = Guid.NewGuid();
        CreatedByUserId = createdByUserId;
        Name = normalizedName;
        Description = description;
        UnitOfMeasure = normalizedUnitOfMeasure;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza um suplemento de catálogo.</summary>
    public void Update(
        string name,
        string? description,
        string? unitOfMeasure,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Supplement name cannot be empty.");

        var normalizedName = name.Trim();
        var normalizedUnitOfMeasure = unitOfMeasure?.Trim();
        ValidateParameters(normalizedName, normalizedUnitOfMeasure);

        Name = normalizedName;
        Description = description;
        UnitOfMeasure = normalizedUnitOfMeasure;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do suplemento.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros do suplemento.</summary>
    private void ValidateParameters(string name, string? unitOfMeasure)
    {
        if (name.Length > 255)
            throw new DomainException("Supplement name cannot exceed 255 characters.");
        if (unitOfMeasure != null && unitOfMeasure.Length > 50)
            throw new DomainException("Unit of measure cannot exceed 50 characters.");
    }
}
