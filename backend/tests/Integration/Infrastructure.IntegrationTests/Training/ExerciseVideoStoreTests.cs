using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Identity;
using Domain.Entities.Training;
using Domain.Exceptions;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Training;

/// <summary>
/// Prova em PostgreSQL real as escritas HTTP de vídeos geridos: atomicidade com os
/// durable jobs, ownership, um upload em curso por exercício e quota serializada
/// sob concorrência.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExerciseVideoStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public ExerciseVideoStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_WritesThePendingVideoAndTheScheduledCleanupAtomically()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var video = NewVideo(exerciseId, tenant.TrainerId);

        var status = await RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, video, maxVideos: 20);

        Assert.Equal(ExerciseVideoRegistrationStatus.Registered, status);
        Assert.Equal("pending", await _fixture.QueryScalarAsync<string>(
            "SELECT status FROM exercise_videos WHERE id = @id", Token, new NpgsqlParameter("id", video.Id)));
        Assert.Equal(Now.AddMinutes(75), await _fixture.QueryScalarAsync<DateTime>(
            "SELECT scheduled_at FROM durable_jobs WHERE idempotency_key = @key AND trainer_id = @trainer",
            Token,
            new NpgsqlParameter("key", ExerciseVideoJobs.ExpireIdempotencyKey(video.Id)),
            new NpgsqlParameter("trainer", tenant.TrainerId)));
    }

    [Fact]
    public async Task Register_WhenExerciseBelongsToAnotherTenant_ReturnsNotFoundWithoutWriting()
    {
        var owner = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var intruder = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(owner.TrainerId);
        var video = NewVideo(exerciseId, intruder.TrainerId);

        var status = await RegisterAsync(intruder.TrainerId, ExerciseVideoCatalog.Private, video, maxVideos: 20);

        Assert.Equal(ExerciseVideoRegistrationStatus.ExerciseNotFound, status);
        Assert.Equal(0L, await CountVideosAsync(exerciseId));
    }

    [Fact]
    public async Task Register_WhenExerciseIsBlocked_IsRefused()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        await _fixture.ExecuteSqlAsync(
            "UPDATE exercises SET platform_enforcement_status = 'blocked', platform_enforcement_reason = 'malicious_content', platform_enforced_at = now() WHERE id = @id",
            Token, new NpgsqlParameter("id", exerciseId));

        var status = await RegisterAsync(
            tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(exerciseId, tenant.TrainerId), maxVideos: 20);

        Assert.Equal(ExerciseVideoRegistrationStatus.ExerciseBlocked, status);
    }

    [Fact]
    public async Task Register_TwoConcurrentUploadsForTheSameExercise_AcceptExactlyOne()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);

        var outcomes = await Task.WhenAll(
            RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(exerciseId, tenant.TrainerId), 20),
            RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(exerciseId, tenant.TrainerId), 20));

        Assert.Equal(
            [ExerciseVideoRegistrationStatus.Registered, ExerciseVideoRegistrationStatus.UploadInProgress],
            outcomes.Order());
        Assert.Equal(1L, await CountVideosAsync(exerciseId));
    }

    [Fact]
    public async Task Register_QuotaCountsExercisesSoAReplacementNeverNeedsAnExtraSlot()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var withVideo = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var withoutVideo = await SeedPrivateExerciseAsync(tenant.TrainerId);
        await SeedReadyVideoAsync(withVideo, tenant.TrainerId);

        var replacement = await RegisterAsync(
            tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(withVideo, tenant.TrainerId), maxVideos: 1);
        var extra = await RegisterAsync(
            tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(withoutVideo, tenant.TrainerId), maxVideos: 1);

        Assert.Equal(
            (ExerciseVideoRegistrationStatus.Registered, ExerciseVideoRegistrationStatus.QuotaExceeded),
            (replacement, extra));
    }

    [Fact]
    public async Task Register_ConcurrentUploadsAtTheQuotaLimit_AcceptExactlyOne()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var first = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var second = await SeedPrivateExerciseAsync(tenant.TrainerId);

        var outcomes = await Task.WhenAll(
            RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(first, tenant.TrainerId), 1),
            RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, NewVideo(second, tenant.TrainerId), 1));

        Assert.Equal(
            [ExerciseVideoRegistrationStatus.Registered, ExerciseVideoRegistrationStatus.QuotaExceeded],
            outcomes.Order());
    }

    [Fact]
    public async Task Register_ForGlobalCatalog_HasNoQuotaAndWritesTheAuditEntry()
    {
        var superuserId = await SeedSuperuserAsync();
        var exerciseId = await SeedGlobalExerciseAsync(superuserId);
        var video = NewVideo(exerciseId, null, superuserId);

        var status = await RegisterAsync(null, ExerciseVideoCatalog.Global, video, maxVideos: 1, superuserId);

        Assert.Equal(ExerciseVideoRegistrationStatus.Registered, status);
        Assert.Equal(1L, await _fixture.QueryScalarAsync<long>(
            "SELECT count(*) FROM administrative_audit_entries WHERE resource_type = 'exercise_video' AND resource_id = @id AND action = 'request_upload'",
            Token, new NpgsqlParameter("id", video.Id)));
    }

    [Fact]
    public async Task MarkUploaded_SchedulesProcessingOnceAndIsIdempotent()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var video = NewVideo(exerciseId, tenant.TrainerId);
        await RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, video, 20);
        var completion = Completion(exerciseId, video.Id, tenant.TrainerId);

        var first = await MarkUploadedAsync(tenant.TrainerId, completion);
        var second = await MarkUploadedAsync(tenant.TrainerId, completion with { CorrelationId = Guid.NewGuid() });

        Assert.Equal(
            (ExerciseVideoUploadTransitionStatus.Applied, ExerciseVideoUploadTransitionStatus.AlreadyApplied),
            (first.Status, second.Status));
        Assert.Equal(1L, await CountJobsAsync(ExerciseVideoJobs.ProcessIdempotencyKey(video.Id)));
        Assert.Equal("processing", await StatusAsync(video.Id));
    }

    [Fact]
    public async Task MarkUploaded_ForAnotherTenant_ReturnsNotFound()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var intruder = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var video = NewVideo(exerciseId, tenant.TrainerId);
        await RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, video, 20);

        var outcome = await MarkUploadedAsync(
            intruder.TrainerId, Completion(exerciseId, video.Id, intruder.TrainerId));

        Assert.Equal(ExerciseVideoUploadTransitionStatus.NotFound, outcome.Status);
        Assert.Equal("pending", await StatusAsync(video.Id));
    }

    [Fact]
    public async Task MarkUploaded_WhenStoredSizeDiffersFromTheDeclaration_ReturnsInvalidStateWithoutScheduling()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var video = NewVideo(exerciseId, tenant.TrainerId);
        await RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, video, 20);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var outcome = await Store(context).MarkUploadedAsync(
            Completion(exerciseId, video.Id, tenant.TrainerId), 2048, "etag-1", Token);

        Assert.Equal(ExerciseVideoUploadTransitionStatus.InvalidState, outcome.Status);
        Assert.Equal("pending", await StatusAsync(video.Id));
        Assert.Equal(0L, await CountJobsAsync(ExerciseVideoJobs.ProcessIdempotencyKey(video.Id)));
    }

    [Fact]
    public async Task MarkUploaded_AfterTheUploadWindowClosed_ReturnsUploadWindowClosedWithoutScheduling()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var video = NewVideo(exerciseId, tenant.TrainerId);
        await RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, video, 20);

        var outcome = await MarkUploadedAsync(
            tenant.TrainerId,
            Completion(exerciseId, video.Id, tenant.TrainerId) with { Now = Now.AddMinutes(30) });

        Assert.Equal(ExerciseVideoUploadTransitionStatus.UploadWindowClosed, outcome.Status);
        Assert.Equal("pending", await StatusAsync(video.Id));
        Assert.Equal(0L, await CountJobsAsync(ExerciseVideoJobs.ProcessIdempotencyKey(video.Id)));
    }

    [Fact]
    public async Task DeletingAnotherTenantsVideo_IsRejectedByTheWriteInterceptor()
    {
        var owner = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var intruder = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(owner.TrainerId);
        var ready = await SeedReadyVideoAsync(exerciseId, owner.TrainerId);

        await using var context = _fixture.CreateContext(intruder.TrainerId);
        context.ExerciseVideos.Remove(ready);

        await Assert.ThrowsAsync<DomainException>(() => context.SaveChangesAsync(Token));
        Assert.Equal(1L, await CountVideosAsync(exerciseId));
    }

    [Fact]
    public async Task RemoveReady_DeletesTheRowAndSchedulesObjectDeletionOnce()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var ready = await SeedReadyVideoAsync(exerciseId, tenant.TrainerId);

        var first = await RemoveAsync(tenant.TrainerId, exerciseId);
        var second = await RemoveAsync(tenant.TrainerId, exerciseId);

        Assert.Equal((ExerciseVideoRemovalStatus.Removed, ExerciseVideoRemovalStatus.NotFound), (first, second));
        Assert.Equal(0L, await CountVideosAsync(exerciseId));
        Assert.Equal(1L, await CountJobsAsync(ExerciseVideoJobs.DeleteObjectIdempotencyKey(ready.Id)));
    }

    [Fact]
    public async Task Database_RejectsASecondReadyVideoForTheSameExercise()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        await SeedReadyVideoAsync(exerciseId, tenant.TrainerId);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => SeedReadyVideoAsync(exerciseId, tenant.TrainerId));

        Assert.Equal("uq_exercise_videos_ready", Assert.IsType<PostgresException>(exception.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Database_RejectsProcessingWithoutAConfirmedUpload()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId);
        var video = NewVideo(exerciseId, tenant.TrainerId);
        await RegisterAsync(tenant.TrainerId, ExerciseVideoCatalog.Private, video, 20);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => _fixture.ExecuteSqlAsync(
            "UPDATE exercise_videos SET status = 'processing' WHERE id = @id", Token, new NpgsqlParameter("id", video.Id)));

        Assert.Equal(("23514", "ck_exercise_videos_upload_confirmed"), (exception.SqlState, exception.ConstraintName));
    }

    [Fact]
    public async Task DeleteGlobalExercise_WithAReadyVideo_ReturnsHasVideo()
    {
        var superuserId = await SeedSuperuserAsync();
        var exerciseId = await SeedGlobalExerciseAsync(superuserId);
        await SeedReadyVideoAsync(exerciseId, null, superuserId);

        await using var context = _fixture.CreateAdministrativeContext(superuserId);
        var outcome = await new GlobalExerciseStore(context, new PostgresConstraintTranslator())
            .DeleteAsync(superuserId, exerciseId, Now, Token);

        Assert.Equal(GlobalExerciseStoreResult.Status.HasVideo, outcome.Kind);
    }

    private async Task<ExerciseVideoRegistrationStatus> RegisterAsync(
        Guid? trainerId,
        ExerciseVideoCatalog catalog,
        ExerciseVideo video,
        int maxVideos,
        Guid? actorUserId = null)
    {
        await using var context = CreateWriterContext(trainerId, actorUserId);
        return await Store(context).RegisterUploadAsync(
            new ExerciseVideoRegistration(
                catalog,
                video,
                actorUserId ?? video.CreatedByUserId,
                Now.AddMinutes(75),
                maxVideos,
                Guid.NewGuid(),
                Now),
            Token);
    }

    private async Task<ExerciseVideoUploadTransition> MarkUploadedAsync(
        Guid trainerId, ExerciseVideoUploadCompletion completion)
    {
        await using var context = _fixture.CreateContext(trainerId);
        return await Store(context).MarkUploadedAsync(completion, 1024, "etag-1", Token);
    }

    private async Task<ExerciseVideoRemovalStatus> RemoveAsync(Guid trainerId, Guid exerciseId)
    {
        await using var context = _fixture.CreateContext(trainerId);
        return await Store(context).RemoveReadyAsync(
            ExerciseVideoCatalog.Private, exerciseId, trainerId, Guid.NewGuid(), Guid.NewGuid(), Now, Token);
    }

    private PtManagerDbContext CreateWriterContext(Guid? trainerId, Guid? actorUserId) =>
        trainerId.HasValue
            ? _fixture.CreateContext(trainerId)
            : _fixture.CreateAdministrativeContext(actorUserId!.Value);

    private static ExerciseVideoStore Store(PtManagerDbContext context) =>
        new(context, new PostgresConstraintTranslator());

    private static ExerciseVideo NewVideo(Guid exerciseId, Guid? owner, Guid? creator = null) =>
        new(exerciseId, owner, "video/mp4", 1024, creator ?? Guid.NewGuid(), Now.AddMinutes(15), Now);

    private static ExerciseVideoUploadCompletion Completion(Guid exerciseId, Guid videoId, Guid trainerId) =>
        new(ExerciseVideoCatalog.Private, exerciseId, videoId, trainerId, Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(1));

    private async Task<Guid> SeedPrivateExerciseAsync(Guid trainerId)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var exercise = IntegrationTestData.Exercise(trainerId, Now);
        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(Token);
        return exercise.Id;
    }

    private async Task<Guid> SeedSuperuserAsync()
    {
        var user = new User(
            new EmailAddress($"superuser-{Guid.NewGuid():N}@example.test"), "superuser", "Superuser", Now);
        user.SetPasswordHash("integration-test-password-hash", Now);

        await using var context = _fixture.CreateContext((Guid?)null);
        context.Users.Add(user);
        await context.SaveChangesAsync(Token);
        return user.Id;
    }

    private async Task<Guid> SeedGlobalExerciseAsync(Guid superuserId)
    {
        await using var context = _fixture.CreateAdministrativeContext(superuserId);
        var created = await new GlobalExerciseStore(context, new PostgresConstraintTranslator())
            .CreateAsync(superuserId, "Global squat", null, null, null, null, null, Now, Token);
        return created.Exercise!.Id;
    }

    private async Task<ExerciseVideo> SeedReadyVideoAsync(Guid exerciseId, Guid? owner, Guid? superuserId = null)
    {
        var video = NewVideo(exerciseId, owner, superuserId);
        video.MarkUploaded(1024, "etag-ready", Now);
        video.MarkReady(10_000, 1280, 720, "avc1", "mp4a", Now);

        await using var context = CreateWriterContext(owner, superuserId);
        context.ExerciseVideos.Add(video);
        await context.SaveChangesAsync(Token);
        return video;
    }

    private Task<long> CountVideosAsync(Guid exerciseId) =>
        _fixture.QueryScalarAsync<long>(
            "SELECT count(*) FROM exercise_videos WHERE exercise_id = @id", Token, new NpgsqlParameter("id", exerciseId));

    private Task<long> CountJobsAsync(string idempotencyKey) =>
        _fixture.QueryScalarAsync<long>(
            "SELECT count(*) FROM durable_jobs WHERE idempotency_key = @key", Token, new NpgsqlParameter("key", idempotencyKey));

    private async Task<string> StatusAsync(Guid videoId) =>
        (await _fixture.QueryScalarAsync<string>(
            "SELECT status FROM exercise_videos WHERE id = @id", Token, new NpgsqlParameter("id", videoId)))!;
}
