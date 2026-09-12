namespace Application.Common.Abstractions;

/// <summary>Porta de armazenamento de media externo (Cloudinary).</summary>
public interface IMediaStorage
{
    Task<MediaUploadOutcome> UploadAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken);

    Task<MediaDeletionOutcome> DeleteAsync(
        string publicId,
        Guid trainerId,
        CancellationToken cancellationToken);
}
