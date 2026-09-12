using Application.Common.Abstractions;

namespace Application.Common.Media;

/// <summary>
/// Orquestra a preparação de uma imagem gerida: normalização, moderação e
/// armazenamento no storage externo.
/// </summary>
public sealed class MediaPreparationPipeline
{
    private readonly IImageProcessor _processor;
    private readonly IImageModerationService _moderation;
    private readonly IMediaStorage _storage;

    public MediaPreparationPipeline(
        IImageProcessor processor,
        IImageModerationService moderation,
        IMediaStorage storage)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _moderation = moderation ?? throw new ArgumentNullException(nameof(moderation));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>Prepara e publica a imagem ou explica porque não o fez.</summary>
    public async Task<MediaPreparationOutcome> PrepareAsync(
        MediaUpload upload,
        ImageProfile profile,
        MediaAssetKind kind,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(profile);

        var processing = await _processor.NormalizeAsync(upload, profile, cancellationToken);
        if (processing.Image is null)
            return MediaPreparationOutcome.InvalidImage(
                processing.Failure ?? ImageValidationFailure.NotDecodable);

        var image = processing.Image;

        if (profile.RequiresModeration)
        {
            var moderation = await _moderation.ReviewAsync(
                new ImageModerationRequest(
                    image.Content,
                    image.ContentType),
                cancellationToken);

            switch (moderation.Verdict)
            {
                case ImageModerationVerdict.Approved:
                    break;
                case ImageModerationVerdict.Rejected:
                    return MediaPreparationOutcome.From(MediaPreparationStatus.Rejected);
                case ImageModerationVerdict.ReviewRequired:
                    return MediaPreparationOutcome.From(MediaPreparationStatus.ReviewRequired);
                default:
                    // Inclui Unavailable e qualquer valor futuro: sem veredicto
                    // explícito de aprovação, não se publica.
                    return MediaPreparationOutcome.From(
                        MediaPreparationStatus.ModerationUnavailable);
            }
        }

        var stored = await _storage.UploadAsync(
            new MediaUploadRequest(
                image.Content,
                image.ContentType,
                kind,
                trainerId),
            cancellationToken);

        return stored.Status switch
        {
            MediaStorageStatus.Success when stored.Media is not null =>
                MediaPreparationOutcome.Prepared(stored.Media),
            MediaStorageStatus.Disabled =>
                MediaPreparationOutcome.From(MediaPreparationStatus.StorageDisabled),
            _ => MediaPreparationOutcome.From(MediaPreparationStatus.StorageFailed)
        };
    }
}
