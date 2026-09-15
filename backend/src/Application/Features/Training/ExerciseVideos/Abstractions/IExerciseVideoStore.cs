using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>
/// Persistência transacional das escritas HTTP de vídeos geridos. Cada mutação
/// grava os Durable Jobs correspondentes na mesma transação.
/// </summary>
public interface IExerciseVideoStore
{
    Task<ExerciseVideoRegistrationStatus> RegisterUploadAsync(
        ExerciseVideoRegistration registration,
        CancellationToken cancellationToken);

    Task<ExerciseVideo?> FindUploadAsync(
        ExerciseVideoCatalog catalog,
        Guid exerciseId,
        Guid videoId,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken);

    Task<ExerciseVideoUploadTransition> MarkUploadedAsync(
        ExerciseVideoUploadCompletion completion,
        long storedSizeBytes,
        string storedETag,
        CancellationToken cancellationToken);

    Task<ExerciseVideoUploadTransition> RejectUploadAsync(
        ExerciseVideoUploadCompletion completion,
        string failureCode,
        CancellationToken cancellationToken);

    Task<ExerciseVideoRemovalStatus> RemoveReadyAsync(
        ExerciseVideoCatalog catalog,
        Guid exerciseId,
        Guid? ownerTrainerId,
        Guid actorUserId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken);
}
