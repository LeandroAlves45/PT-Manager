using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;
using Infrastructure.Jobs;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Prova o consumo das eliminações de imagem: o tenant vem do envelope
/// persistido, o payload é allowlist fechada, e as falhas são classificadas para
/// retry ou dead letter.
/// </summary>
public sealed class MediaDeletionOutboxHandlerTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();

    [Fact]
    public void Handlers_ExposeTheTypesWrittenByTheStores()
    {
        var storage = new RecordingStorage(MediaStorageStatus.Success);

        Assert.Equal("trainer-logo.delete", new TrainerLogoDeletionOutboxHandler(storage).MessageType);
        Assert.Equal("client-avatar.delete", new ClientAvatarDeletionOutboxHandler(storage).MessageType);
    }

    [Fact]
    public async Task Handle_DeletesWithTheEnvelopeTenantNotAPayloadValue()
    {
        var storage = new RecordingStorage(MediaStorageStatus.Success);

        var outcome = await new ClientAvatarDeletionOutboxHandler(storage).HandleAsync(
            Envelope("""{"public_id":"pt-manager/trainers/x/avatars/a1"}""", TrainerId),
            TestContext.Current.CancellationToken);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(("pt-manager/trainers/x/avatars/a1", TrainerId), (storage.PublicId, storage.TrainerId));
    }

    [Fact]
    public async Task Handle_WhenEnvelopeHasNoTenant_FailsPermanentlyWithoutIo()
    {
        var storage = new RecordingStorage(MediaStorageStatus.Success);

        var outcome = await new TrainerLogoDeletionOutboxHandler(storage).HandleAsync(
            Envelope("""{"public_id":"p"}""", null), TestContext.Current.CancellationToken);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("media_deletion_tenant_missing", outcome.FailureCode);
        Assert.Null(storage.PublicId);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"public_id":""}""")]
    [InlineData("""{"public_id":"p","trainer_id":"00000000-0000-0000-0000-000000000001"}""")]
    public async Task Handle_WhenPayloadIsNotTheClosedShape_FailsPermanently(string payload)
    {
        var storage = new RecordingStorage(MediaStorageStatus.Success);

        var outcome = await new TrainerLogoDeletionOutboxHandler(storage).HandleAsync(
            Envelope(payload, TrainerId), TestContext.Current.CancellationToken);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("media_deletion_payload_invalid", outcome.FailureCode);
        Assert.Null(storage.PublicId);
    }

    [Theory]
    [InlineData(MediaStorageStatus.TransientFailure, DispatchItemOutcomeKind.TransientFailure)]
    [InlineData(MediaStorageStatus.Disabled, DispatchItemOutcomeKind.TransientFailure)]
    [InlineData(MediaStorageStatus.InvalidResponse, DispatchItemOutcomeKind.TransientFailure)]
    [InlineData(MediaStorageStatus.PermanentFailure, DispatchItemOutcomeKind.PermanentFailure)]
    public async Task Handle_ClassifiesStorageOutcomes(MediaStorageStatus status, DispatchItemOutcomeKind expected)
    {
        var storage = new RecordingStorage(status);

        var outcome = await new TrainerLogoDeletionOutboxHandler(storage).HandleAsync(
            Envelope("""{"public_id":"p"}""", TrainerId), TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome.Kind);
    }

    private static OutboxMessageEnvelope Envelope(string payload, Guid? trainerId) =>
        new(Guid.NewGuid(), trainerId, "trainer-logo.delete", payload,
            $"trainer-logo.delete:{Guid.NewGuid():N}", Guid.NewGuid(), 0, Guid.NewGuid());

    private sealed class RecordingStorage(MediaStorageStatus deleteStatus) : IMediaStorage
    {
        public string? PublicId { get; private set; }
        public Guid? TrainerId { get; private set; }

        public Task<MediaUploadOutcome> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Deletion handlers never upload.");

        public Task<MediaDeletionOutcome> DeleteAsync(string publicId, Guid trainerId, CancellationToken cancellationToken)
        {
            (PublicId, TrainerId) = (publicId, trainerId);
            return Task.FromResult(new MediaDeletionOutcome(deleteStatus, "storage_code"));
        }
    }
}
