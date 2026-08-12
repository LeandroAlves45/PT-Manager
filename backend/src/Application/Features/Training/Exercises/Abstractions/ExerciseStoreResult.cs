using Domain.Entities.Training;

namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>Representa resultados esperados de uma mutação de Exercise.</summary>
public sealed class ExerciseStoreResult
{
    public enum Status
    {
        Updated,
        Changed,
        AlreadyInRequestedState,
        NotFound,
        GlobalReadOnly
    }

    public Status Kind { get; }
    public Exercise? Exercise { get; }

    private ExerciseStoreResult(Status kind, Exercise? exercise)
    {
        Kind = kind;
        Exercise = exercise;
    }

    public static ExerciseStoreResult ForChanged() => new(Status.Changed, null);
    public static ExerciseStoreResult ForAlreadyRequested() =>
        new(Status.AlreadyInRequestedState, null);
    public static ExerciseStoreResult ForNotFound() => new(Status.NotFound, null);
    public static ExerciseStoreResult ForGlobalReadOnly() => new(Status.GlobalReadOnly, null);
}
