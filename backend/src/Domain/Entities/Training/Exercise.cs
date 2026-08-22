using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Exercício do catálogo, global ou privado do Personal Trainer.
/// </summary>
public sealed class Exercise
{
    public Guid Id { get; private set; }
    public Guid? OwnerTrainerId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? MuscleGroups { get; private set; }
    public string? Equipment { get; private set; }
    public string? DifficultyLevel { get; private set; }
    public string? VideoUrl { get; private set; }
    public bool IsActive { get; private set; }
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
        if (ownerTrainerId.HasValue && ownerTrainerId.Value == Guid.Empty)
            throw new DomainException("Owner trainer ID cannot be empty.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        SetFields(name, description, muscleGroups, equipment, difficultyLevel, videoUrl);
        IsActive = true;
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
        SetFields(name, description, muscleGroups, equipment, difficultyLevel, videoUrl);
        UpdatedAt = now;
    }

    /// <summary>Controla a disponibilidade sem perder referências históricas.</summary>
    public void SetActive(bool isActive, DateTime now)
    {
        if (IsActive == isActive)
            return;
        IsActive = isActive;
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
        if ((muscleGroups is not null && muscleGroups.Trim().Length > 500) ||
            (equipment is not null && equipment.Trim().Length > 255) ||
            (difficultyLevel is not null && difficultyLevel.Trim().Length > 50) ||
            (videoUrl is not null && videoUrl.Trim().Length > 500))
        {
            throw new DomainException("Exercise fields exceed their maximum length.");
        }

        Name = normalizedName;
        Description = NormalizeOptional(description);
        MuscleGroups = NormalizeOptional(muscleGroups);
        Equipment = NormalizeOptional(equipment);
        DifficultyLevel = NormalizeOptional(difficultyLevel);
        VideoUrl = NormalizeOptional(videoUrl);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
