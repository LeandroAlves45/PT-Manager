using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Exercício do catálogo, global ou privado do Personal Trainer.
/// </summary>
public class Exercise
{
    public Guid Id { get; private set; }
    public Guid? OwnerTrainerId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>
    /// Grupos musculares em CSV simples (ex: "peito,ombro,tríceps")
    /// </summary>
    public string? MuscleGroups { get; private set; }
    public string? Equipment { get; private set; }
    public string? DifficultyLevel { get; private set; }
    public string? VideoUrl { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Exercise() { }

    /// <summary>
    /// Cria um exercício do catálogo.
    /// </summary>
    public Exercise(
        Guid? ownerTrainerId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now
    )
    {
        SetFields(name, description, muscleGroups, equipment, difficultyLevel, videoUrl);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza os campos descritivos do exercício.</summary>
    public void Update(
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now
    )
    {
        EnsureNotDeleted();
        SetFields(name, description, muscleGroups, equipment, difficultyLevel, videoUrl);
        UpdatedAt = now;
    }

    /// <summary>Controla a disponibilidade sem perder referências históricas.</summary>
    public void SetActive(bool isActive, DateTime now)
    {
        EnsureNotDeleted();
        IsActive = isActive;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do exercício, marcando-o como excluído.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros de criação/atualização do exercício.</summary>
    private void SetFields(
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is 0 or > 255)
            throw new DomainException("Exercise name must be between 1 and 255 characters.");
        if (muscleGroups is { Length: > 500 } || equipment is { Length: > 255 } ||
            difficultyLevel is { Length: > 50 } || videoUrl is { Length: > 500 })
            throw new DomainException("Exercise fields exceed their maximum length.");

        Name = normalizedName;
        Description = NormalizeOptional(description);
        MuscleGroups = NormalizeOptional(muscleGroups);
        Equipment = NormalizeOptional(equipment);
        DifficultyLevel = NormalizeOptional(difficultyLevel);
        VideoUrl = NormalizeOptional(videoUrl);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted exercise.");
    }
}
