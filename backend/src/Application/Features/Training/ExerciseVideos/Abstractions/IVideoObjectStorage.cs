namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>
/// Porta do storage privado de vídeos. Todas as operações recebem o owner
/// confiávél para que o adapter recuse identificadores fora do espaço do tenant.
/// </summary>
public interface IVideoObjectStorage
{
    Task<VideoUploadAuthorizationOutcome> CreateUploadAuthorizationAsync(
        string objectKey,
        Guid? ownerTrainerId,
        string contentType,
        long contentLength,
        DateTime expiresAt,
        CancellationToken cancellationToken);

    Task<VideoObjectInfoOutcome> GetObjectInfoAsync(
        string objectKey,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken);

    Task<VideoPlaybackUrlOutcome> CreatePlaybackUrlAsync(
        string objectKey,
        Guid? ownerTrainerId,
        DateTime expiresAt,
        CancellationToken cancellationToken);

    Task<VideoObjectDeletionOutcome> DeleteAsync(
        string objectKey,
        Guid? ownerTrainerId,
        CancellationToken cancellationToken);
}
