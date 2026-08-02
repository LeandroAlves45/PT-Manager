using Domain.Exceptions;

namespace Domain.Entities.Supplements;

/// <summary>
/// Suplemento do catálogo, global ou criado pelo personal trainer.
/// </summary>
public class Supplement
{
    public Guid Id { get; private set; }
    /// <summary>
    /// Proprietário do suplemento: null identifica  uma linha global autorizada.
    /// </summary>
    public Guid? OwnerTrainerId { get; private set; }
    /// <summary>Autor da criação; não concede visibilidade nem autorização</summary>
    public Guid? CreatedByUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Unidade de medida: "grams", "ml", "capsules", etc.</summary>
    public string? UnitOfMeasure { get; private set; }
    public string? ServingSize { get; private set; }
    public string? Timing { get; private set; }
    public string? TrainerNotes { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Supplement() { }

    /// <summary>Cria um suplemento de catálogo.</summary>
    public Supplement(
        Guid? ownerTrainerId,
        Guid? createdByUserId,
        string name,
        string? description,
        string? unitOfMeasure,
        string? servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now
    )
    {
        SetFields(name, description, unitOfMeasure, servingSize, timing, trainerNotes);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        CreatedByUserId = createdByUserId;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza um suplemento de catálogo.</summary>
    public void Update(
        string name,
        string? description,
        string? unitOfMeasure,
        string? servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now
    )
    {
        EnsureNotDeleted();
        SetFields(name, description, unitOfMeasure, servingSize, timing, trainerNotes);
        UpdatedAt = now;
    }

    /// <summary>Soft delete do suplemento.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros do suplemento.</summary>
    private void SetFields(
        string name,
        string? description,
        string? unitOfMeasure,
        string? servingSize,
        string? timing,
        string? trainerNotes
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is 0 or > 255)
            throw new DomainException("Supplement name must be between 1 and 255 characters.");
        if (unitOfMeasure is { Length: > 50 } || servingSize is { Length: > 100 } ||
            timing is { Length: > 255 })
            throw new DomainException("Supplement fields exceed their maximum length.");

        Name = normalizedName;
        Description = NormalizeOptional(description);
        UnitOfMeasure = NormalizeOptional(unitOfMeasure);
        ServingSize = NormalizeOptional(servingSize);
        Timing = NormalizeOptional(timing);
        TrainerNotes = NormalizeOptional(trainerNotes);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted supplement.");
    }
}
