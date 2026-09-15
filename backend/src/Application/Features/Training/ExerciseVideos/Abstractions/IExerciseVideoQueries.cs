namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>Leituras de reprodução segregadas por audiência.</summary>
public interface IExerciseVideoQueries
{
    Task<ExerciseVideoPlaybackCandidate?> FindPlaybackCandidateAsync(
        ExerciseVideoPlaybackAudience audience,
        Guid exerciseId,
        Guid? trainerId,
        Guid? clientUserId,
        CancellationToken cancellationToken);
}
