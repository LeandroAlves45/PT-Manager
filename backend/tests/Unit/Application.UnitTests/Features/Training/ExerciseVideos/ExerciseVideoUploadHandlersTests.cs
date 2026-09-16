using Application.Features.Training;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.CompleteExerciseVideoUpload;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoUpload;
using Application.Features.Training.ExerciseVideos.RemoveExerciseVideo;
using Application.Features.Training.ExerciseVideos.RequestExerciseVideoUpload;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Training.ExerciseVideos;

/// <summary>
/// Prova a autorização, o lifecycle HTTP e a ausência de I/O desnecessário nos
/// casos de uso de upload: o tamanho confirmado vem sempre do fornecedor e o
/// owner vem sempre do contexto autenticado.
/// </summary>
public sealed class ExerciseVideoUploadHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------ Request upload

    [Theory]
    [InlineData("video/webm", 1024L, "exercise_video_content_type_unsupported")]
    [InlineData("video/mp4", 0L, "exercise_video_size_invalid")]
    [InlineData("video/mp4", 104_857_601L, "exercise_video_size_invalid")]
    public async Task Request_WhenDeclarationIsInvalid_ReturnsValidationWithoutIo(
        string contentType, long size, string expectedCode)
    {
        var (handler, storage, store) = RequestHandler(VideoTenantContext.Trainer(TrainerId));

        var result = await handler.HandleAsync(
            new RequestExerciseVideoUploadCommand(ExerciseVideoCatalog.Private, Guid.NewGuid(), contentType, size),
            Token);

        Assert.Contains(result.Error!.ValidationErrors, error => error.Code == expectedCode);
        Assert.Empty(storage.Calls);
        Assert.Null(store.LastRegistration);
    }

    [Fact]
    public async Task Request_ForPrivateCatalog_UsesTheAuthenticatedTenantAsOwner()
    {
        var context = VideoTenantContext.Trainer(TrainerId);
        var (handler, storage, store) = RequestHandler(context);
        var exerciseId = Guid.NewGuid();

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Private, exerciseId), Token);

        Assert.True(result.IsSuccess);
        var registration = store.LastRegistration!;
        Assert.Equal((TrainerId, exerciseId, context.UserId!.Value),
            (registration.Video.OwnerTrainerId!.Value, registration.Video.ExerciseId, registration.ActorUserId));
        Assert.Equal(registration.Video.ObjectKey, storage.LastAuthorization!.Value.Key);
        Assert.Equal(TrainerId, storage.LastAuthorization.Value.Owner);
    }

    [Fact]
    public async Task Request_ComputesTheApprovedWindowsFromTheClock()
    {
        var (handler, storage, store) = RequestHandler(VideoTenantContext.Trainer(TrainerId));

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Private), Token);

        var expiresAt = VideoTestData.Now.AddMinutes(15);
        Assert.Equal(expiresAt, storage.LastAuthorization!.Value.ExpiresAt);
        Assert.Equal(expiresAt.AddHours(1), store.LastRegistration!.CleanupScheduledAt);
        Assert.Equal(20, store.LastRegistration.MaxVideosPerTrainer);
        Assert.Equal(("PUT", "video/mp4", ExerciseVideoPolicy.MaxSizeBytes),
            (result.Value.UploadMethod, result.Value.UploadContentType, result.Value.MaxSizeBytes));
        Assert.Equal("pending", result.Value.Video.Status);
    }

    [Fact]
    public async Task Request_ForGlobalCatalog_HasNoOwner()
    {
        var (handler, _, store) = RequestHandler(VideoTenantContext.Administrator());

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Global), Token);

        Assert.True(result.IsSuccess);
        Assert.Null(store.LastRegistration!.Video.OwnerTrainerId);
        Assert.Equal("global", result.Value.Video.Scope);
    }

    [Fact]
    public async Task Request_WhenTrainerTargetsTheGlobalCatalog_IsForbiddenWithoutIo()
    {
        var (handler, storage, store) = RequestHandler(VideoTenantContext.Trainer(TrainerId));

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Global), Token);

        Assert.Equal(TrainingErrors.AdministratorOnly.Code, result.Error!.Code);
        Assert.Empty(storage.Calls);
        Assert.Null(store.LastRegistration);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("superuser")]
    public async Task Request_ForPrivateCatalog_RejectsNonTrainers(string role)
    {
        var context = new VideoTenantContext(TrainerId, Guid.NewGuid(), role, role == "superuser");
        var (handler, storage, _) = RequestHandler(context);

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Private), Token);

        Assert.Equal(TrainingErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Empty(storage.Calls);
    }

    [Theory]
    [InlineData(VideoStorageStatus.Disabled)]
    [InlineData(VideoStorageStatus.PermanentFailure)]
    public async Task Request_WhenStorageCannotAuthorize_FailsWithoutReservingQuota(VideoStorageStatus status)
    {
        var (handler, storage, store) = RequestHandler(VideoTenantContext.Trainer(TrainerId));
        storage.AuthorizationStatus = status;

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Private), Token);

        Assert.Equal(ExerciseVideoErrors.StorageUnavailable.Code, result.Error!.Code);
        Assert.Null(store.LastRegistration);
    }

    [Theory]
    [InlineData(ExerciseVideoRegistrationStatus.ExerciseNotFound, "exercise_not_found")]
    [InlineData(ExerciseVideoRegistrationStatus.ExerciseInactive, "exercise_video_exercise_inactive")]
    [InlineData(ExerciseVideoRegistrationStatus.ExerciseBlocked, "exercise_video_exercise_blocked")]
    [InlineData(ExerciseVideoRegistrationStatus.UploadInProgress, "exercise_video_upload_in_progress")]
    [InlineData(ExerciseVideoRegistrationStatus.QuotaExceeded, "exercise_video_quota_exceeded")]
    public async Task Request_MapsStoreOutcomesToStableCodes(
        ExerciseVideoRegistrationStatus status, string expectedCode)
    {
        var (handler, _, store) = RequestHandler(VideoTenantContext.Trainer(TrainerId));
        store.RegistrationResult = status;

        var result = await handler.HandleAsync(ValidRequest(ExerciseVideoCatalog.Private), Token);

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    // ------------------------------------------------------------ Complete upload

    [Fact]
    public async Task Complete_WhenUploadDoesNotExistForTheOwner_ReturnsNotFoundWithoutIo()
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));

        var result = await handler.HandleAsync(CompleteCommand(Guid.NewGuid(), Guid.NewGuid()), Token);

        Assert.Equal(ExerciseVideoErrors.UploadNotFound.Code, result.Error!.Code);
        Assert.Equal(TrainerId, store.LastFind!.Value.Owner);
        Assert.Empty(storage.Calls);
    }

    [Fact]
    public async Task Complete_WhenAlreadyProcessing_IsIdempotentWithoutProviderCalls()
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Processing(TrainerId);

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal("processing", result.Value.Status);
        Assert.Empty(storage.Calls);
        Assert.Null(store.LastMarkUploaded);
    }

    [Fact]
    public async Task Complete_WhenUploadWasRejected_ReturnsClosed()
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Pending(TrainerId);
        store.Upload.Reject("exercise_video_size_mismatch", VideoTestData.Now);

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal(ExerciseVideoErrors.UploadClosed.Code, result.Error!.Code);
        Assert.Empty(storage.Calls);
    }

    [Fact]
    public async Task Complete_AfterTheUploadWindow_ReturnsExpiredWithoutProviderCalls()
    {
        var clock = new VideoClock(VideoTestData.Now.AddMinutes(16));
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId), clock);
        store.Upload = VideoTestData.Pending(TrainerId);

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal(ExerciseVideoErrors.UploadExpired.Code, result.Error!.Code);
        Assert.Empty(storage.Calls);
    }

    [Fact]
    public async Task Complete_WhenObjectIsNotInStorage_ReturnsIncompleteAndKeepsPending()
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Pending(TrainerId);
        storage.InfoOutcome = new VideoObjectInfoOutcome(VideoStorageStatus.NotFound);

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal(ExerciseVideoErrors.UploadIncomplete.Code, result.Error!.Code);
        Assert.Null(store.LastMarkUploaded);
        Assert.Null(store.LastReject);
        Assert.Equal(ExerciseVideoStatus.Pending, store.Upload.Status);
    }

    [Theory]
    [InlineData(VideoStorageStatus.Disabled)]
    [InlineData(VideoStorageStatus.TransientFailure)]
    public async Task Complete_WhenProviderIsUnavailable_ReturnsDependencyFailure(VideoStorageStatus status)
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Pending(TrainerId);
        storage.InfoOutcome = new VideoObjectInfoOutcome(status);

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal(ExerciseVideoErrors.StorageUnavailable.Code, result.Error!.Code);
        Assert.Null(store.LastMarkUploaded);
    }

    [Theory]
    [InlineData(1025L, "video/mp4", "exercise_video_size_mismatch")]
    [InlineData(1023L, "video/mp4", "exercise_video_size_mismatch")]
    [InlineData(1024L, "video/quicktime", "exercise_video_content_type_mismatch")]
    [InlineData(1024L, null, "exercise_video_content_type_mismatch")]
    public async Task Complete_WhenStoredObjectDiffersFromTheAuthorization_RejectsAndNeverProcesses(
        long storedSize, string? storedContentType, string expectedFailureCode)
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Pending(TrainerId, size: 1024);
        storage.InfoOutcome = new VideoObjectInfoOutcome(
            VideoStorageStatus.Success, new VideoObjectInfo(storedSize, "etag-9", storedContentType));

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal(ExerciseVideoErrors.UploadRejected.Code, result.Error!.Code);
        Assert.Equal(expectedFailureCode, store.LastReject!.Value.FailureCode);
        Assert.Null(store.LastMarkUploaded);
    }

    [Fact]
    public async Task Complete_WhenStoredObjectMatches_PersistsTheProviderSizeAndETag()
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Pending(TrainerId, size: 1024);
        storage.InfoOutcome = new VideoObjectInfoOutcome(
            VideoStorageStatus.Success,
            new VideoObjectInfo(1024, "provider-etag", "video/mp4; charset=utf-8"));

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal("processing", result.Value.Status);
        Assert.Equal((1024L, "provider-etag"), (store.LastMarkUploaded!.Value.Size, store.LastMarkUploaded.Value.ETag));
        Assert.Equal(TrainerId, store.LastMarkUploaded.Value.Completion.OwnerTrainerId);
        Assert.Equal((store.Upload.ObjectKey, (Guid?)TrainerId), storage.LastHead!.Value);
    }

    [Theory]
    [InlineData(ExerciseVideoUploadTransitionStatus.NotFound, "exercise_video_upload_not_found")]
    [InlineData(ExerciseVideoUploadTransitionStatus.UploadWindowClosed, "exercise_video_upload_expired")]
    [InlineData(ExerciseVideoUploadTransitionStatus.InvalidState, "exercise_video_state_conflict")]
    public async Task Complete_MapsConcurrentStoreOutcomes(
        ExerciseVideoUploadTransitionStatus status, string expectedCode)
    {
        var (handler, storage, store) = CompleteHandler(VideoTenantContext.Trainer(TrainerId));
        store.Upload = VideoTestData.Pending(TrainerId, size: 1024);
        store.MarkUploadedResult = status;
        storage.InfoOutcome = new VideoObjectInfoOutcome(
            VideoStorageStatus.Success, new VideoObjectInfo(1024, "etag", "video/mp4"));

        var result = await handler.HandleAsync(CompleteCommand(store.Upload), Token);

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    // ------------------------------------------------------------ Get upload and remove

    [Fact]
    public async Task GetUpload_ForAnotherOwner_IsIndistinguishableFromMissing()
    {
        var store = new FakeExerciseVideoStore();
        var handler = new GetExerciseVideoUploadHandler(VideoTenantContext.Trainer(TrainerId), store);

        var result = await handler.HandleAsync(
            new GetExerciseVideoUploadQuery(ExerciseVideoCatalog.Private, Guid.NewGuid(), Guid.NewGuid()), Token);

        Assert.Equal(ExerciseVideoErrors.UploadNotFound.Code, result.Error!.Code);
        Assert.Equal(TrainerId, store.LastFind!.Value.Owner);
    }

    [Fact]
    public async Task Remove_UsesTheAuthenticatedOwnerAndMapsNotFound()
    {
        var store = new FakeExerciseVideoStore { RemovalResult = ExerciseVideoRemovalStatus.NotFound };
        var context = VideoTenantContext.Trainer(TrainerId);
        var handler = new RemoveExerciseVideoHandler(context, new VideoClock(VideoTestData.Now), store);

        var result = await handler.HandleAsync(
            new RemoveExerciseVideoCommand(ExerciseVideoCatalog.Private, Guid.NewGuid()), Token);

        Assert.Equal(ExerciseVideoErrors.VideoNotFound.Code, result.Error!.Code);
        Assert.Equal((TrainerId, context.UserId!.Value),
            (store.LastRemoval!.Value.Owner!.Value, store.LastRemoval.Value.ActorUserId));
    }

    [Fact]
    public async Task Remove_ForGlobalCatalog_RequiresTheAdministrativeContext()
    {
        var store = new FakeExerciseVideoStore();
        var notAdministrative = new VideoTenantContext(null, Guid.NewGuid(), "superuser", false);
        var handler = new RemoveExerciseVideoHandler(notAdministrative, new VideoClock(VideoTestData.Now), store);

        var result = await handler.HandleAsync(
            new RemoveExerciseVideoCommand(ExerciseVideoCatalog.Global, Guid.NewGuid()), Token);

        Assert.Equal(TrainingErrors.AdministratorOnly.Code, result.Error!.Code);
        Assert.Null(store.LastRemoval);
    }

    private static RequestExerciseVideoUploadCommand ValidRequest(
        ExerciseVideoCatalog catalog, Guid? exerciseId = null) =>
        new(catalog, exerciseId ?? Guid.NewGuid(), "video/mp4", 1024);

    private static CompleteExerciseVideoUploadCommand CompleteCommand(Domain.Entities.Training.ExerciseVideo video) =>
        new(ExerciseVideoCatalog.Private, video.ExerciseId, video.Id);

    private static CompleteExerciseVideoUploadCommand CompleteCommand(Guid exerciseId, Guid videoId) =>
        new(ExerciseVideoCatalog.Private, exerciseId, videoId);

    private static (RequestExerciseVideoUploadHandler, FakeVideoStorage, FakeExerciseVideoStore) RequestHandler(
        VideoTenantContext context)
    {
        var storage = new FakeVideoStorage();
        var store = new FakeExerciseVideoStore();
        return (new RequestExerciseVideoUploadHandler(
            new RequestExerciseVideoUploadCommandValidator(),
            context,
            new VideoClock(VideoTestData.Now),
            VideoTestData.Settings(),
            storage,
            store), storage, store);
    }

    private static (CompleteExerciseVideoUploadHandler, FakeVideoStorage, FakeExerciseVideoStore) CompleteHandler(
        VideoTenantContext context, VideoClock? clock = null)
    {
        var storage = new FakeVideoStorage();
        var store = new FakeExerciseVideoStore();
        return (new CompleteExerciseVideoUploadHandler(
            context,
            clock ?? new VideoClock(VideoTestData.Now),
            storage,
            store), storage, store);
    }
}
