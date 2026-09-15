using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos.Abstractions;

namespace Application.Features.Training.ExerciseVideos.Processing;

/// <summary>Traduz o resultado de uma transição condicionada pelo lease.</summary>
internal static class ExerciseVideoJobOutcomes
{
    internal static DispatchItemOutcome FromTransition(ExerciseVideoJobTransitionStatus status) =>
        status switch
        {
            ExerciseVideoJobTransitionStatus.Applied or
            ExerciseVideoJobTransitionStatus.AlreadyTerminal or
            ExerciseVideoJobTransitionStatus.NotFound => DispatchItemOutcome.Succeeded(),
            ExerciseVideoJobTransitionStatus.LeaseLost => DispatchItemOutcome.LeaseLost(),
            ExerciseVideoJobTransitionStatus.InvalidState =>
                DispatchItemOutcome.PermanentFailure("exercise_video_state_invalid"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    internal static bool IsSupported(DurableJobEnvelope job, string jobType) =>
        job.JobType == jobType && job.JobVersion == ExerciseVideoJobs.Version;
}
