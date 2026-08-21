using Domain.Entities.Training;

namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>Representa outcomes esperados da administração global de exercícios.</summary>
public sealed class GlobalExerciseStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        Changed,
        Deleted,
        AlreadyInRequestedState,
        NotFound,
        Inactive,
        Referenced,
        HasReferences
    }

    public Status Kind { get; }
    public Exercise? Exercise { get; }

    private GlobalExerciseStoreResult(Status kind, Exercise? exercise)
    {
        Kind = kind;
        Exercise = exercise;
    }

    public static GlobalExerciseStoreResult WithExercise(Status kind, Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return new GlobalExerciseStoreResult(kind, exercise);
    }

    public static GlobalExerciseStoreResult For(Status kind) => new(kind, null);
}
