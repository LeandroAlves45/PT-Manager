namespace Application.Common.Abstractions;

/// <summary>Porta de armazenamento de media externo (Cloudinary).</summary>
public interface IMediaStorage
{
    Task<StoredMedia> UploadAsync(MediaUpload upload, CancellationToken cancellationToken);

    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}

/// <summary>Referência de um asset já persistido no storage externo.</summary>
public sealed record StoredMedia(string Url, string PublicId);
