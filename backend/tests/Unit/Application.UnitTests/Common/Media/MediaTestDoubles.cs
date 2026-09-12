using Application.Common.Abstractions;
using Application.Common.Media;

namespace Application.UnitTests.Common.Media;

/// <summary>
/// Duplos partilhados pelas portas de media. Registam a ordem das chamadas num
/// único diário para que os testes provem a sequência, e não apenas que cada
/// porta foi chamada.
/// </summary>
internal sealed class MediaCallJournal
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Record(string call) => _calls.Add(call);
}

internal sealed class FakeImageProcessor(MediaCallJournal journal) : IImageProcessor
{
    public ImageValidationFailure? Failure { get; init; }

    public Task<ImageProcessingResult> NormalizeAsync(
        MediaUpload upload,
        ImageProfile profile,
        CancellationToken cancellationToken)
    {
        journal.Record("normalize");
        return Task.FromResult(Failure is { } failure
            ? ImageProcessingResult.Invalid(failure)
            : ImageProcessingResult.Ok(new ProcessedImage(new byte[] { 1, 2, 3 }, "image/webp", 512, 512)));
    }
}

internal sealed class FakeModerationService(MediaCallJournal journal) : IImageModerationService
{
    public ImageModerationVerdict Verdict { get; init; } = ImageModerationVerdict.Approved;

    public ReadOnlyMemory<byte>? ReviewedContent { get; private set; }

    public Task<ImageModerationResult> ReviewAsync(
        ImageModerationRequest request,
        CancellationToken cancellationToken)
    {
        journal.Record("moderate");
        ReviewedContent = request.Content;
        return Task.FromResult(new ImageModerationResult(Verdict));
    }
}

internal sealed class FakeMediaStorage(MediaCallJournal journal) : IMediaStorage
{
    public const string UploadedUrl = "https://res.cloudinary.com/demo/image/upload/new.webp";
    public const string UploadedPublicId = "pt-manager/trainers/t/logos/new";

    public MediaStorageStatus UploadStatus { get; init; } = MediaStorageStatus.Success;
    public MediaStorageStatus DeleteStatus { get; init; } = MediaStorageStatus.Success;
    public bool ThrowOnDelete { get; init; }

    public MediaUploadRequest? UploadedRequest { get; private set; }
    public string? DeletedPublicId { get; private set; }
    public Guid? DeletedForTrainer { get; private set; }
    public CancellationToken? DeleteToken { get; private set; }

    public Task<MediaUploadOutcome> UploadAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken)
    {
        journal.Record("upload");
        UploadedRequest = request;
        return Task.FromResult(UploadStatus == MediaStorageStatus.Success
            ? new MediaUploadOutcome(MediaStorageStatus.Success, new StoredMedia(UploadedUrl, UploadedPublicId))
            : new MediaUploadOutcome(UploadStatus, FailureCode: "fake_failure"));
    }

    public Task<MediaDeletionOutcome> DeleteAsync(
        string publicId,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        journal.Record("delete");
        DeletedPublicId = publicId;
        DeletedForTrainer = trainerId;
        DeleteToken = cancellationToken;

        if (ThrowOnDelete)
            throw new InvalidOperationException("Delete failed.");

        return Task.FromResult(new MediaDeletionOutcome(DeleteStatus));
    }
}

/// <summary>Monta um pipeline real sobre duplos, partilhando o mesmo diário.</summary>
internal sealed class MediaHarness
{
    public MediaHarness(
        ImageValidationFailure? processingFailure = null,
        ImageModerationVerdict verdict = ImageModerationVerdict.Approved,
        MediaStorageStatus uploadStatus = MediaStorageStatus.Success,
        MediaStorageStatus deleteStatus = MediaStorageStatus.Success,
        bool throwOnDelete = false)
    {
        Processor = new FakeImageProcessor(Journal) { Failure = processingFailure };
        Moderation = new FakeModerationService(Journal) { Verdict = verdict };
        Storage = new FakeMediaStorage(Journal)
        {
            UploadStatus = uploadStatus,
            DeleteStatus = deleteStatus,
            ThrowOnDelete = throwOnDelete
        };
        Pipeline = new MediaPreparationPipeline(Processor, Moderation, Storage);
    }

    public MediaCallJournal Journal { get; } = new();
    public FakeImageProcessor Processor { get; }
    public FakeModerationService Moderation { get; }
    public FakeMediaStorage Storage { get; }
    public MediaPreparationPipeline Pipeline { get; }

    public static MediaUpload ValidUpload() =>
        new(new MemoryStream([1, 2, 3]), "image/png", 3);
}
