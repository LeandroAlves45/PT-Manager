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
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Exercise name cannot be empty.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        Name = name.Trim();
        Description = description;
        MuscleGroups = muscleGroups;
        Equipment = equipment;
        DifficultyLevel = difficultyLevel;
        VideoUrl = videoUrl;
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
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Exercise name cannot be empty.");

        Name = name.Trim();
        Description = description;
        MuscleGroups = muscleGroups;
        Equipment = equipment;
        DifficultyLevel = difficultyLevel;
        VideoUrl = videoUrl;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do exercício, marcando-o como excluído.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }
}
