using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseSetLogs.Abstractions;

/// <summary>Classifica resultados funcionais de escrita de logs.</summary>
public sealed class ExerciseSetLogStoreResult
{
    public enum Status
    {
        Recorded,
        Corrected,
        NotFound,
        TrainingPlanInactive,
        SetNotFound,
        PerformedAtInFuture,
        PerformedAtOutsidePlan
    }

    public Status Kind { get; }
    public ClientExerciseSetLog? Log { get; }

    private ExerciseSetLogStoreResult(Status kind, ClientExerciseSetLog? log)
    {
        Kind = kind;
        Log = log;
    }

    public static ExerciseSetLogStoreResult ForRecorded(ClientExerciseSetLog log) =>
        WithLog(Status.Recorded, log);
    public static ExerciseSetLogStoreResult ForCorrected(ClientExerciseSetLog log) =>
        WithLog(Status.Corrected, log);
    public static ExerciseSetLogStoreResult ForNotFound() =>
        new(Status.NotFound, null);
    public static ExerciseSetLogStoreResult ForTrainingPlanInactive() =>
        new(Status.TrainingPlanInactive, null);
    public static ExerciseSetLogStoreResult ForSetNotFound() =>
        new(Status.SetNotFound, null);
    public static ExerciseSetLogStoreResult ForPerformedAtInFuture() =>
        new(Status.PerformedAtInFuture, null);
    public static ExerciseSetLogStoreResult ForPerformedAtOutsidePlan() =>
        new(Status.PerformedAtOutsidePlan, null);

    private static ExerciseSetLogStoreResult WithLog(Status status, ClientExerciseSetLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return new ExerciseSetLogStoreResult(status, log);
    }
}
