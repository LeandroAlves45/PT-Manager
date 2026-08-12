using Domain.Entities.Training;

namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>Persiste mutações tenant-safe do catálogo de exercícios.</summary>
public interface IExerciseStore
{
    /// <summary>Adiciona um novo exercício.</summary>
    Task AddAsync(Exercise exercise, CancellationToken cancellationToken);

    /// <summary>Atualiza um exercício existente.</summary>
    Task<ExerciseStoreResult> UpdateAsync(
        Guid exerciseId,
        Guid trainerId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        CancellationToken cancellationToken
    );

    /// <summary>Ativa ou arquiva um exercício existente.</summary>
    Task<ExerciseStoreResult> SetActivityAsync(
        Guid exerciseId,
        Guid trainerId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );
}
