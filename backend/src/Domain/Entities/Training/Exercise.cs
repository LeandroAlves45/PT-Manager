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
            throw new DomainException("Exercise name is required");

        var normalizedName = name.Trim();
        var normalizedMuscleGroups = muscleGroups?.Trim();
        var normalizedEquipment = equipment?.Trim();
        var normalizedDifficultyLevel = difficultyLevel?.Trim();
        var normalizedVideoUrl = videoUrl?.Trim();
        ValidateParametersExercise(
            normalizedName,
            normalizedMuscleGroups,
            normalizedEquipment,
            normalizedDifficultyLevel,
            normalizedVideoUrl
        );

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        Name = normalizedName;
        Description = description;
        MuscleGroups = normalizedMuscleGroups;
        Equipment = normalizedEquipment;
        DifficultyLevel = normalizedDifficultyLevel;
        VideoUrl = normalizedVideoUrl;
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
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Exercise name is required");

        var normalizedName = name.Trim();
        var normalizedMuscleGroups = muscleGroups?.Trim();
        var normalizedEquipment = equipment?.Trim();
        var normalizedDifficultyLevel = difficultyLevel?.Trim();
        var normalizedVideoUrl = videoUrl?.Trim();
        ValidateParametersExercise(
            normalizedName,
            normalizedMuscleGroups,
            normalizedEquipment,
            normalizedDifficultyLevel,
            normalizedVideoUrl
        );

        Name = normalizedName;
        Description = description;
        MuscleGroups = normalizedMuscleGroups;
        Equipment = normalizedEquipment;
        DifficultyLevel = normalizedDifficultyLevel;
        VideoUrl = normalizedVideoUrl;
        UpdatedAt = now;
    }

    /// <summary>Soft delete do exercício, marcando-o como excluído.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros de criação/atualização do exercício.</summary>
    private void ValidateParametersExercise(
        string name,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl
    )
    {
        if (name.Length > 255)
            throw new DomainException("Exercise name cannot exceed 255 characters");
        if (muscleGroups != null && muscleGroups.Length > 500)
            throw new DomainException("Exercise muscle groups cannot exceed 500 characters");
        if (equipment != null && equipment.Length > 255)
            throw new DomainException("Exercise equipment cannot exceed 255 characters");
        if (difficultyLevel != null && difficultyLevel.Length > 50)
            throw new DomainException("Exercise difficulty level cannot exceed 50 characters");
        if (videoUrl != null && videoUrl.Length > 500)
            throw new DomainException("Exercise video URL cannot exceed 500 characters");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot update a deleted exercise.");
    }
}
