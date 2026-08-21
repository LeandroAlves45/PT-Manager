namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>Persiste mutações globais de exercícios e a respetiva auditoria na mesma transação.</summary>
public interface IGlobalExerciseStore
{
    Task<GlobalExerciseStoreResult> CreateAsync(
        Guid actorUserId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalExerciseStoreResult> UpdateAsync(
        Guid actorUserId,
        Guid exerciseId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalExerciseStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid exerciseId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalExerciseStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid exerciseId,
        DateTime now,
        CancellationToken cancellationToken
    );
}
