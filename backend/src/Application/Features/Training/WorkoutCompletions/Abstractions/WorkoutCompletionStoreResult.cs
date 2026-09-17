using Domain.Entities.Training;

namespace Application.Features.Training.WorkoutCompletions.Abstractions;

/// <summary>Classifica o resultado de concluir um treino.</summary>
public sealed class WorkoutCompletionStoreResult
{
    public enum Status
    {
        Completed,
        AlreadyCompleted,
        NotFound,
        TrainingPlanInactive,
        DateOutsidePlan
    }

    public Status Kind { get; }
    public WorkoutCompletion? Completion { get; }

    private WorkoutCompletionStoreResult(Status kind, WorkoutCompletion? completion)
    {
        Kind = kind;
        Completion = completion;
    }

    public static WorkoutCompletionStoreResult ForCompleted(WorkoutCompletion completion) =>
        new(Status.Completed, completion ?? throw new ArgumentNullException(nameof(completion)));

    public static WorkoutCompletionStoreResult ForAlreadyCompleted(WorkoutCompletion completion) =>
        new(Status.AlreadyCompleted, completion ?? throw new ArgumentNullException(nameof(completion)));

    public static WorkoutCompletionStoreResult For(Status kind)
    {
        if (kind is Status.Completed or Status.AlreadyCompleted)
            throw new ArgumentException("Completed results require the completion.", nameof(kind));
        return new(kind, null);
    }
}
