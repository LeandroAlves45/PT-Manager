using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>
/// Transições executadas por durable jobs. Cada escrita confirma, na mesma
/// transação, que o job ainda detém o lease: um worker que o perdeu nunca publica
/// nem recusa um vídeo.
/// </summary>
public interface IExerciseVideoProcessingStore
{
    Task<ExerciseVideo?> FindAsync(
        Guid videoId,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken);

    Task<ExerciseVideoJobTransitionStatus> MarkReadyAsync(
        ExerciseVideoLease lease,
        VideoTechnicalMetadata metadata,
        CancellationToken cancellationToken);

    Task<ExerciseVideoJobTransitionStatus> TerminateAsync(
        ExerciseVideoLease lease,
        ExerciseVideoTermination termination,
        string failureCode,
        CancellationToken cancellationToken);
}
