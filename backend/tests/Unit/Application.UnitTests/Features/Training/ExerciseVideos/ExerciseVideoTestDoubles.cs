using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;

namespace Application.UnitTests.Features.Training.ExerciseVideos;

/// <summary>Dados e instantes fixos dos testes de vídeo gerido.</summary>
internal static class VideoTestData
{
    public static readonly DateTime Now = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

    public static ExerciseVideoSettings Settings() => new(
        20, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1));

    public static ExerciseVideo Pending(
        Guid? owner,
        Guid? exerciseId = null,
        long size = 1024,
        string contentType = "video/mp4") =>
        new(exerciseId ?? Guid.NewGuid(), owner, contentType, size, Guid.NewGuid(), Now.AddMinutes(15), Now);

    public static ExerciseVideo Processing(Guid? owner, string contentType = "video/mp4")
    {
        var video = Pending(owner, contentType: contentType);
        video.MarkUploaded(1024, "etag-1", Now.AddMinutes(1));
        return video;
    }

    public static VideoTechnicalMetadata AcceptedMetadata() =>
        new(VideoContainer.Mp4, 30_000, 1280, 720, "avc1", "mp4a", 1, 1);

    public static DurableJobEnvelope Job(string jobType, string payload, Guid? trainerId, int version = 1) =>
        new(Guid.NewGuid(), trainerId, jobType, version, payload, "key", Guid.NewGuid(), 1, Guid.NewGuid());
}

internal sealed class VideoTenantContext(Guid? trainerId, Guid? userId, string? role, bool isAdministrative)
    : ITenantContext
{
    public Guid? TrainerId => trainerId;
    public Guid? UserId => userId;
    public string? Role => role;
    public TenantOrigin Origin => TenantOrigin.Http;
    public bool IsAdministrative => isAdministrative;

    public static VideoTenantContext Trainer(Guid trainerId) => new(trainerId, Guid.NewGuid(), "trainer", false);
    public static VideoTenantContext Client(Guid trainerId) => new(trainerId, Guid.NewGuid(), "client", false);
    public static VideoTenantContext Administrator() => new(null, Guid.NewGuid(), "superuser", true);
}

internal sealed class VideoClock(DateTime now) : IClock
{
    public DateTime UtcNow { get; set; } = now;
}

internal sealed class FakeVideoStorage : IVideoObjectStorage
{
    public VideoStorageStatus AuthorizationStatus { get; set; } = VideoStorageStatus.Success;
    public VideoObjectInfoOutcome InfoOutcome { get; set; } = new(VideoStorageStatus.NotFound);
    public VideoStorageStatus PlaybackStatus { get; set; } = VideoStorageStatus.Success;
    public VideoStorageStatus DeletionStatus { get; set; } = VideoStorageStatus.Success;

    public List<string> Calls { get; } = [];
    public (string Key, Guid? Owner, string ContentType, long Length, DateTime ExpiresAt)? LastAuthorization { get; private set; }
    public (string Key, Guid? Owner)? LastHead { get; private set; }
    public (string Key, Guid? Owner, DateTime ExpiresAt)? LastPlayback { get; private set; }
    public (string Key, Guid? Owner)? LastDeletion { get; private set; }

    public Task<VideoUploadAuthorizationOutcome> CreateUploadAuthorizationAsync(
        string objectKey, Guid? ownerTrainerId, string contentType, long contentLength,
        DateTime expiresAt, CancellationToken cancellationToken)
    {
        Calls.Add("authorize");
        LastAuthorization = (objectKey, ownerTrainerId, contentType, contentLength, expiresAt);
        return Task.FromResult(AuthorizationStatus == VideoStorageStatus.Success
            ? new VideoUploadAuthorizationOutcome(
                VideoStorageStatus.Success,
                new VideoUploadAuthorization(
                    new Uri("https://r2.test/upload"), "PUT", contentType, contentLength, expiresAt))
            : new VideoUploadAuthorizationOutcome(AuthorizationStatus, FailureCode: "stub_failure"));
    }

    public Task<VideoObjectInfoOutcome> GetObjectInfoAsync(
        string objectKey, Guid? ownerTrainerId, CancellationToken cancellationToken)
    {
        Calls.Add("head");
        LastHead = (objectKey, ownerTrainerId);
        return Task.FromResult(InfoOutcome);
    }

    public Task<VideoPlaybackUrlOutcome> CreatePlaybackUrlAsync(
        string objectKey, Guid? ownerTrainerId, DateTime expiresAt, CancellationToken cancellationToken)
    {
        Calls.Add("playback");
        LastPlayback = (objectKey, ownerTrainerId, expiresAt);
        return Task.FromResult(PlaybackStatus == VideoStorageStatus.Success
            ? new VideoPlaybackUrlOutcome(
                VideoStorageStatus.Success, new VideoPlaybackUrl(new Uri("https://r2.test/play"), expiresAt))
            : new VideoPlaybackUrlOutcome(PlaybackStatus, FailureCode: "stub_failure"));
    }

    public Task<VideoObjectDeletionOutcome> DeleteAsync(
        string objectKey, Guid? ownerTrainerId, CancellationToken cancellationToken)
    {
        Calls.Add("delete");
        LastDeletion = (objectKey, ownerTrainerId);
        return Task.FromResult(new VideoObjectDeletionOutcome(
            DeletionStatus,
            DeletionStatus == VideoStorageStatus.Success ? null : "stub_failure"));
    }
}

internal sealed class FakeExerciseVideoStore : IExerciseVideoStore
{
    public ExerciseVideoRegistrationStatus RegistrationResult { get; set; } = ExerciseVideoRegistrationStatus.Registered;
    public ExerciseVideo? Upload { get; set; }
    public ExerciseVideoUploadTransitionStatus MarkUploadedResult { get; set; } = ExerciseVideoUploadTransitionStatus.Applied;
    public ExerciseVideoUploadTransitionStatus RejectResult { get; set; } = ExerciseVideoUploadTransitionStatus.Applied;
    public ExerciseVideoRemovalStatus RemovalResult { get; set; } = ExerciseVideoRemovalStatus.Removed;

    public ExerciseVideoRegistration? LastRegistration { get; private set; }
    public (ExerciseVideoCatalog Catalog, Guid ExerciseId, Guid VideoId, Guid? Owner)? LastFind { get; private set; }
    public (ExerciseVideoUploadCompletion Completion, long Size, string ETag)? LastMarkUploaded { get; private set; }
    public (ExerciseVideoUploadCompletion Completion, string FailureCode)? LastReject { get; private set; }
    public (ExerciseVideoCatalog Catalog, Guid ExerciseId, Guid? Owner, Guid ActorUserId)? LastRemoval { get; private set; }

    public Task<ExerciseVideoRegistrationStatus> RegisterUploadAsync(
        ExerciseVideoRegistration registration, CancellationToken cancellationToken)
    {
        LastRegistration = registration;
        return Task.FromResult(RegistrationResult);
    }

    public Task<ExerciseVideo?> FindUploadAsync(
        ExerciseVideoCatalog catalog, Guid exerciseId, Guid videoId, Guid? ownerTrainerId,
        CancellationToken cancellationToken)
    {
        LastFind = (catalog, exerciseId, videoId, ownerTrainerId);
        return Task.FromResult(Upload);
    }

    public Task<ExerciseVideoUploadTransition> MarkUploadedAsync(
        ExerciseVideoUploadCompletion completion, long storedSizeBytes, string storedETag,
        CancellationToken cancellationToken)
    {
        LastMarkUploaded = (completion, storedSizeBytes, storedETag);
        if (MarkUploadedResult == ExerciseVideoUploadTransitionStatus.Applied && Upload is not null)
            Upload.MarkUploaded(storedSizeBytes, storedETag, completion.Now);
        return Task.FromResult(new ExerciseVideoUploadTransition(MarkUploadedResult, Upload));
    }

    public Task<ExerciseVideoUploadTransition> RejectUploadAsync(
        ExerciseVideoUploadCompletion completion, string failureCode, CancellationToken cancellationToken)
    {
        LastReject = (completion, failureCode);
        if (RejectResult == ExerciseVideoUploadTransitionStatus.Applied && Upload is not null)
            Upload.Reject(failureCode, completion.Now);
        return Task.FromResult(new ExerciseVideoUploadTransition(RejectResult, Upload));
    }

    public Task<ExerciseVideoRemovalStatus> RemoveReadyAsync(
        ExerciseVideoCatalog catalog, Guid exerciseId, Guid? ownerTrainerId, Guid actorUserId,
        Guid correlationId, DateTime now, CancellationToken cancellationToken)
    {
        LastRemoval = (catalog, exerciseId, ownerTrainerId, actorUserId);
        return Task.FromResult(RemovalResult);
    }
}

internal sealed class FakeExerciseVideoQueries : IExerciseVideoQueries
{
    public ExerciseVideoPlaybackCandidate? Candidate { get; set; }

    public (ExerciseVideoPlaybackAudience Audience, Guid ExerciseId, Guid? TrainerId, Guid? ClientUserId)? LastQuery
    {
        get;
        private set;
    }

    public Task<ExerciseVideoPlaybackCandidate?> FindPlaybackCandidateAsync(
        ExerciseVideoPlaybackAudience audience, Guid exerciseId, Guid? trainerId, Guid? clientUserId,
        CancellationToken cancellationToken)
    {
        LastQuery = (audience, exerciseId, trainerId, clientUserId);
        return Task.FromResult(Candidate);
    }
}

internal sealed class FakeProcessingStore : IExerciseVideoProcessingStore
{
    public ExerciseVideo? Video { get; set; }
    public ExerciseVideoJobTransitionStatus MarkReadyResult { get; set; } = ExerciseVideoJobTransitionStatus.Applied;
    public ExerciseVideoJobTransitionStatus TerminateResult { get; set; } = ExerciseVideoJobTransitionStatus.Applied;

    public Guid? LastFindOwner { get; private set; }
    public bool FindWasCalled { get; private set; }
    public (ExerciseVideoLease Lease, VideoTechnicalMetadata Metadata)? LastMarkReady { get; private set; }
    public (ExerciseVideoLease Lease, ExerciseVideoTermination Termination, string FailureCode)? LastTerminate { get; private set; }

    public Task<ExerciseVideo?> FindAsync(Guid videoId, Guid? ownerTrainerId, CancellationToken cancellationToken)
    {
        FindWasCalled = true;
        LastFindOwner = ownerTrainerId;
        return Task.FromResult(Video is not null && Video.Id == videoId ? Video : null);
    }

    public Task<ExerciseVideoJobTransitionStatus> MarkReadyAsync(
        ExerciseVideoLease lease, VideoTechnicalMetadata metadata, CancellationToken cancellationToken)
    {
        LastMarkReady = (lease, metadata);
        return Task.FromResult(MarkReadyResult);
    }

    public Task<ExerciseVideoJobTransitionStatus> TerminateAsync(
        ExerciseVideoLease lease, ExerciseVideoTermination termination, string failureCode,
        CancellationToken cancellationToken)
    {
        LastTerminate = (lease, termination, failureCode);
        return Task.FromResult(TerminateResult);
    }
}

internal sealed class FakeProbe : IVideoMetadataProbe
{
    public VideoProbeOutcome Outcome { get; set; } = VideoProbeOutcome.Probed(VideoTestData.AcceptedMetadata());
    public int Calls { get; private set; }
    public (string Key, Guid? Owner, long Size, string ETag)? LastProbe { get; private set; }

    public Task<VideoProbeOutcome> ProbeAsync(
        string objectKey, Guid? ownerTrainerId, long sizeBytes, string eTag, CancellationToken cancellationToken)
    {
        Calls++;
        LastProbe = (objectKey, ownerTrainerId, sizeBytes, eTag);
        return Task.FromResult(Outcome);
    }
}
