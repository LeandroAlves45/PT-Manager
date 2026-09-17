using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>Classifica o resultado das escritas de séries feitas pelo cliente.</summary>
public sealed class MyExerciseSetLogStoreResult
{
    public enum Status
    {
        Recorded,
        Corrected,
        Deleted,
        NotFound,
        TrainingPlanInactive,
        SetNotFound,
        DateOutsidePlan,
        NotEditable,
        WorkoutAlreadyCompleted
    }

    public Status Kind { get; }
    public ClientExerciseSetLog? Log { get; }

    private MyExerciseSetLogStoreResult(Status kind, ClientExerciseSetLog? log)
    {
        Kind = kind;
        Log = log;
    }

    public bool IsSuccess => Kind is Status.Recorded or Status.Corrected or Status.Deleted;

    public static MyExerciseSetLogStoreResult ForRecorded(ClientExerciseSetLog log) =>
        new(Status.Recorded, log ?? throw new ArgumentNullException(nameof(log)));

    public static MyExerciseSetLogStoreResult ForCorrected(ClientExerciseSetLog log) =>
        new(Status.Corrected, log ?? throw new ArgumentNullException(nameof(log)));

    public static MyExerciseSetLogStoreResult For(Status kind)
    {
        if (kind is Status.Recorded or Status.Corrected)
            throw new ArgumentException("Recorded and corrected results require a log.", nameof(kind));

        return new(kind, null);
    }
}
