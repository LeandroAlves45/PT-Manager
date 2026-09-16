using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.Processing;
using Domain.Entities.Training;

namespace Application.UnitTests.Features.Training.ExerciseVideos;

/// <summary>
/// Prova os três durable jobs de vídeo: validação técnica, limpeza de abandono e
/// eliminação do objeto. A perda de lease nunca publica, o owner vem do job e o
/// payload é uma allowlist fechada.
/// </summary>
public sealed class ExerciseVideoJobHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Handlers_ArePlatformCapableAndExposeTheApprovedRoutes()
    {
        IDurableJobHandler[] handlers =
        [
            new ProcessExerciseVideoJobHandler(new FakeProcessingStore(), new FakeProbe()),
            new ExpireExerciseVideoUploadJobHandler(new FakeProcessingStore(), new VideoClock(VideoTestData.Now)),
            new DeleteExerciseVideoObjectJobHandler(new FakeVideoStorage())
        ];

        Assert.All(handlers, handler => Assert.IsAssignableFrom<IPlatformDurableJobHandler>(handler));
        Assert.Equal(
            ["exercise-video.delete-object:v1", "exercise-video.expire:v1", "exercise-video.process:v1"],
            handlers.Select(handler => $"{handler.JobType}:v{handler.JobVersion}").Order());
    }

    // ------------------------------------------------------------ Process

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"video_id":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData("""{"video_id":"6f1c1f10-7b1f-4a31-9a8e-1a2b3c4d5e6f","owner":"x"}""")]
    public async Task Process_WhenPayloadIsNotTheClosedShape_FailsPermanentlyWithoutIo(string payload)
    {
        var store = new FakeProcessingStore();
        var probe = new FakeProbe();

        var outcome = await new ProcessExerciseVideoJobHandler(store, probe).HandleAsync(
            VideoTestData.Job(ExerciseVideoJobs.ProcessType, payload, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.False(store.FindWasCalled);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Process_WhenVersionIsUnknown_FailsPermanently()
    {
        var outcome = await new ProcessExerciseVideoJobHandler(new FakeProcessingStore(), new FakeProbe())
            .HandleAsync(
                VideoTestData.Job(ExerciseVideoJobs.ProcessType, ExerciseVideoJobs.SerializeVideoPayload(Guid.NewGuid()), TrainerId, 2),
                Token);

        Assert.Equal("exercise_video_job_contract_mismatch", outcome.FailureCode);
    }

    [Fact]
    public async Task Process_ReadsTheVideoWithTheJobTenant()
    {
        var store = new FakeProcessingStore();

        var outcome = await new ProcessExerciseVideoJobHandler(store, new FakeProbe()).HandleAsync(
            ProcessJob(Guid.NewGuid(), TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(TrainerId, store.LastFindOwner);
    }

    [Fact]
    public async Task Process_WhenVideoIsAlreadyFinal_SucceedsWithoutProbing()
    {
        var video = VideoTestData.Processing(TrainerId);
        video.MarkReady(1000, 1280, 720, "avc1", null, VideoTestData.Now);
        var store = new FakeProcessingStore { Video = video };
        var probe = new FakeProbe();

        var outcome = await new ProcessExerciseVideoJobHandler(store, probe).HandleAsync(
            ProcessJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(0, probe.Calls);
        Assert.Null(store.LastMarkReady);
    }

    [Fact]
    public async Task Process_WhenVideoIsAcceptable_PublishesUnderTheJobLease()
    {
        var video = VideoTestData.Processing(TrainerId);
        var store = new FakeProcessingStore { Video = video };
        var probe = new FakeProbe();
        var job = ProcessJob(video.Id, TrainerId);

        var outcome = await new ProcessExerciseVideoJobHandler(store, probe).HandleAsync(job, Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        var lease = store.LastMarkReady!.Value.Lease;
        Assert.Equal((video.Id, (Guid?)TrainerId, job.Id, job.LeaseOwnerId), (lease.VideoId, lease.OwnerTrainerId, lease.JobId, lease.LeaseOwnerId));
        Assert.Equal((video.ObjectKey, (Guid?)TrainerId, 1024L, "etag-1"), probe.LastProbe!.Value);
    }

    [Fact]
    public async Task Process_WhenLeaseIsLost_NeverReportsSuccess()
    {
        var video = VideoTestData.Processing(TrainerId);
        var store = new FakeProcessingStore
        {
            Video = video,
            MarkReadyResult = ExerciseVideoJobTransitionStatus.LeaseLost
        };

        var outcome = await new ProcessExerciseVideoJobHandler(store, new FakeProbe()).HandleAsync(
            ProcessJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.LeaseLost, outcome.Kind);
    }

    [Fact]
    public async Task Process_WhenPolicyRejects_TerminatesWithTheStableCode()
    {
        var video = VideoTestData.Processing(TrainerId);
        var store = new FakeProcessingStore { Video = video };
        var probe = new FakeProbe
        {
            Outcome = VideoProbeOutcome.Probed(VideoTestData.AcceptedMetadata() with { VideoCodec = "hvc1" })
        };

        var outcome = await new ProcessExerciseVideoJobHandler(store, probe).HandleAsync(
            ProcessJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Null(store.LastMarkReady);
        Assert.Equal(
            (ExerciseVideoTermination.Rejected, "exercise_video_codec_unsupported"),
            (store.LastTerminate!.Value.Termination, store.LastTerminate.Value.FailureCode));
    }

    [Theory]
    [InlineData(VideoProbeStatus.Unsupported, "exercise_video_fragmented_unsupported")]
    [InlineData(VideoProbeStatus.ObjectChanged, "exercise_video_object_changed")]
    [InlineData(VideoProbeStatus.NotFound, "exercise_video_object_missing")]
    public async Task Process_WhenContainerCannotBeAccepted_RejectsWithTheProbeCode(
        VideoProbeStatus status, string failureCode)
    {
        var video = VideoTestData.Processing(TrainerId);
        var store = new FakeProcessingStore { Video = video };
        var probe = new FakeProbe { Outcome = VideoProbeOutcome.Failure(status, failureCode) };

        await new ProcessExerciseVideoJobHandler(store, probe).HandleAsync(ProcessJob(video.Id, TrainerId), Token);

        Assert.Equal(failureCode, store.LastTerminate!.Value.FailureCode);
        Assert.Null(store.LastMarkReady);
    }

    [Theory]
    [InlineData(VideoProbeStatus.TransientFailure)]
    [InlineData(VideoProbeStatus.Disabled)]
    public async Task Process_WhenProviderIsUnavailable_RetriesWithoutTransition(VideoProbeStatus status)
    {
        var video = VideoTestData.Processing(TrainerId);
        var store = new FakeProcessingStore { Video = video };
        var probe = new FakeProbe { Outcome = VideoProbeOutcome.Failure(status, "r2_timeout") };

        var outcome = await new ProcessExerciseVideoJobHandler(store, probe).HandleAsync(
            ProcessJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Null(store.LastTerminate);
        Assert.Null(store.LastMarkReady);
    }

    // ------------------------------------------------------------ Expire

    [Fact]
    public async Task Expire_WhenUploadWasAbandoned_FailsTheVideo()
    {
        var video = VideoTestData.Pending(TrainerId);
        var store = new FakeProcessingStore { Video = video };
        var clock = new VideoClock(VideoTestData.Now.AddHours(2));

        var outcome = await new ExpireExerciseVideoUploadJobHandler(store, clock).HandleAsync(
            ExpireJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(
            (ExerciseVideoTermination.Failed, "exercise_video_upload_abandoned"),
            (store.LastTerminate!.Value.Termination, store.LastTerminate.Value.FailureCode));
    }

    [Fact]
    public async Task Expire_WhenProcessingNeverFinished_FailsWithTimeout()
    {
        var video = VideoTestData.Processing(TrainerId);
        var store = new FakeProcessingStore { Video = video };

        await new ExpireExerciseVideoUploadJobHandler(store, new VideoClock(VideoTestData.Now.AddHours(2)))
            .HandleAsync(ExpireJob(video.Id, TrainerId), Token);

        Assert.Equal("exercise_video_processing_timeout", store.LastTerminate!.Value.FailureCode);
    }

    [Fact]
    public async Task Expire_WhileTheWindowIsOpen_RetriesWithoutTouchingTheVideo()
    {
        var video = VideoTestData.Pending(TrainerId);
        var store = new FakeProcessingStore { Video = video };

        var outcome = await new ExpireExerciseVideoUploadJobHandler(store, new VideoClock(VideoTestData.Now))
            .HandleAsync(ExpireJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Null(store.LastTerminate);
    }

    [Fact]
    public async Task Expire_WhenVideoIsReady_IsANoOp()
    {
        var video = VideoTestData.Processing(TrainerId);
        video.MarkReady(1000, 1280, 720, "avc1", null, VideoTestData.Now);
        var store = new FakeProcessingStore { Video = video };

        var outcome = await new ExpireExerciseVideoUploadJobHandler(store, new VideoClock(VideoTestData.Now.AddDays(1)))
            .HandleAsync(ExpireJob(video.Id, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Null(store.LastTerminate);
    }

    // ------------------------------------------------------------ Delete object

    [Fact]
    public async Task Delete_UsesTheJobTenantNotAPayloadValue()
    {
        var storage = new FakeVideoStorage();
        var videoId = Guid.NewGuid();
        var key = ExerciseVideo.BuildObjectKey(TrainerId, videoId);

        var outcome = await new DeleteExerciseVideoObjectJobHandler(storage).HandleAsync(
            DeleteJob(videoId, key, TrainerId), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal((key, (Guid?)TrainerId), storage.LastDeletion!.Value);
    }

    [Fact]
    public async Task Delete_ForGlobalContent_RunsWithoutTenant()
    {
        var storage = new FakeVideoStorage();
        var videoId = Guid.NewGuid();
        var key = ExerciseVideo.BuildObjectKey(null, videoId);

        var outcome = await new DeleteExerciseVideoObjectJobHandler(storage).HandleAsync(
            DeleteJob(videoId, key, null), Token);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Null(storage.LastDeletion!.Value.Owner);
    }

    [Fact]
    public async Task Delete_WhenPayloadKeyBelongsToAnotherTenant_FailsPermanentlyWithoutIo()
    {
        var storage = new FakeVideoStorage();
        var videoId = Guid.NewGuid();

        var otherTenant = await new DeleteExerciseVideoObjectJobHandler(storage).HandleAsync(
            DeleteJob(videoId, ExerciseVideo.BuildObjectKey(Guid.NewGuid(), videoId), TrainerId), Token);
        var globalFromTenantJob = await new DeleteExerciseVideoObjectJobHandler(storage).HandleAsync(
            DeleteJob(videoId, ExerciseVideo.BuildObjectKey(null, videoId), TrainerId), Token);

        Assert.All(new[] { otherTenant, globalFromTenantJob }, outcome =>
            Assert.Equal("exercise_video_object_key_invalid", outcome.FailureCode));
        Assert.Empty(storage.Calls);
    }

    [Theory]
    [InlineData(VideoStorageStatus.Success, DispatchItemOutcomeKind.Succeeded)]
    [InlineData(VideoStorageStatus.NotFound, DispatchItemOutcomeKind.Succeeded)]
    [InlineData(VideoStorageStatus.Disabled, DispatchItemOutcomeKind.TransientFailure)]
    [InlineData(VideoStorageStatus.TransientFailure, DispatchItemOutcomeKind.TransientFailure)]
    [InlineData(VideoStorageStatus.PermanentFailure, DispatchItemOutcomeKind.PermanentFailure)]
    public async Task Delete_ClassifiesStorageOutcomes(VideoStorageStatus status, DispatchItemOutcomeKind expected)
    {
        var storage = new FakeVideoStorage { DeletionStatus = status };
        var videoId = Guid.NewGuid();

        var outcome = await new DeleteExerciseVideoObjectJobHandler(storage).HandleAsync(
            DeleteJob(videoId, ExerciseVideo.BuildObjectKey(TrainerId, videoId), TrainerId), Token);

        Assert.Equal(expected, outcome.Kind);
    }

    private static DurableJobEnvelope ProcessJob(Guid videoId, Guid? trainerId) =>
        VideoTestData.Job(ExerciseVideoJobs.ProcessType, ExerciseVideoJobs.SerializeVideoPayload(videoId), trainerId);

    private static DurableJobEnvelope ExpireJob(Guid videoId, Guid? trainerId) =>
        VideoTestData.Job(ExerciseVideoJobs.ExpireType, ExerciseVideoJobs.SerializeVideoPayload(videoId), trainerId);

    private static DurableJobEnvelope DeleteJob(Guid videoId, string key, Guid? trainerId) =>
        VideoTestData.Job(
            ExerciseVideoJobs.DeleteObjectType,
            ExerciseVideoJobs.SerializeDeleteObjectPayload(videoId, key),
            trainerId);
}
