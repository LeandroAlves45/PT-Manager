using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Jobs;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Training;
using Npgsql;

namespace Infrastructure.IntegrationTests.Training;

/// <summary>
/// Prova em PostgreSQL real que as transições dos jobs de vídeo só são escritas
/// com o lease válido, que a substituição nunca deixa dois vídeos publicados e
/// que um job de plataforma sem tenant só toca em vídeos globais.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExerciseVideoProcessingStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public ExerciseVideoProcessingStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MarkReady_WithAValidLease_PublishesAndSupersedesThePreviousReadyVideo()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"proc-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedExerciseAsync(tenant.TrainerId);
        var previous = await SeedVideoAsync(exerciseId, tenant.TrainerId, ready: true);
        var candidate = await SeedVideoAsync(exerciseId, tenant.TrainerId, ready: false);
        var lease = await SeedClaimedJobAsync(tenant.TrainerId, candidate.Id, TimeSpan.FromMinutes(1));

        var status = await MarkReadyAsync(tenant.TrainerId, lease);

        Assert.Equal(ExerciseVideoJobTransitionStatus.Applied, status);
        Assert.Equal("ready", await StatusAsync(candidate.Id));
        Assert.Equal(0L, await _fixture.QueryScalarAsync<long>(
            "SELECT count(*) FROM exercise_videos WHERE id = @id", Token, new NpgsqlParameter("id", previous.Id)));
        Assert.Equal(1L, await CountJobsAsync(ExerciseVideoJobs.DeleteObjectIdempotencyKey(previous.Id)));
    }

    [Fact]
    public async Task MarkReady_WhenTheLeaseExpired_NeverPublishes()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"proc-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedExerciseAsync(tenant.TrainerId);
        var candidate = await SeedVideoAsync(exerciseId, tenant.TrainerId, ready: false);
        var lease = await SeedClaimedJobAsync(tenant.TrainerId, candidate.Id, TimeSpan.FromSeconds(-1));

        var status = await MarkReadyAsync(tenant.TrainerId, lease);

        Assert.Equal(ExerciseVideoJobTransitionStatus.LeaseLost, status);
        Assert.Equal("processing", await StatusAsync(candidate.Id));
    }

    [Fact]
    public async Task MarkReady_WhenAnotherWorkerOwnsTheLease_NeverPublishes()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"proc-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedExerciseAsync(tenant.TrainerId);
        var candidate = await SeedVideoAsync(exerciseId, tenant.TrainerId, ready: false);
        var lease = await SeedClaimedJobAsync(tenant.TrainerId, candidate.Id, TimeSpan.FromMinutes(1));

        var status = await MarkReadyAsync(tenant.TrainerId, lease with { LeaseOwnerId = Guid.NewGuid() });

        Assert.Equal(ExerciseVideoJobTransitionStatus.LeaseLost, status);
        Assert.Equal("processing", await StatusAsync(candidate.Id));
    }

    [Fact]
    public async Task Terminate_ForGlobalVideo_RunsWithoutTenantAndIsIdempotent()
    {
        var exerciseId = await SeedGlobalExerciseAsync();
        var candidate = await SeedVideoAsync(exerciseId, null, ready: false);
        var lease = await SeedClaimedJobAsync(null, candidate.Id, TimeSpan.FromMinutes(1));

        var first = await TerminateAsync(null, lease);
        var second = await TerminateAsync(null, lease);

        Assert.Equal(
            (ExerciseVideoJobTransitionStatus.Applied, ExerciseVideoJobTransitionStatus.AlreadyTerminal),
            (first, second));
        Assert.Equal("rejected", await StatusAsync(candidate.Id));
        Assert.Equal(1L, await CountJobsAsync(ExerciseVideoJobs.DeleteObjectIdempotencyKey(candidate.Id)));
    }

    [Fact]
    public async Task Find_WithTheWrongOwner_IsIndistinguishableFromMissing()
    {
        var tenant = await _fixture.SeedTenantWithClientAsync($"proc-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedExerciseAsync(tenant.TrainerId);
        var video = await SeedVideoAsync(exerciseId, tenant.TrainerId, ready: false);

        await using var context = _fixture.CreateContext((Guid?)null);
        var store = new ExerciseVideoProcessingStore(context, new TestClock(Now));

        Assert.Null(await store.FindAsync(video.Id, Guid.NewGuid(), Token));
        Assert.Null(await store.FindAsync(video.Id, null, Token));
        Assert.NotNull(await store.FindAsync(video.Id, tenant.TrainerId, Token));
    }

    [Fact]
    public async Task Terminate_ForAnotherTenantVideo_ReturnsNotFoundWithoutWriting()
    {
        var owner = await _fixture.SeedTenantWithClientAsync($"proc-{Guid.NewGuid():N}", Token);
        var intruder = await _fixture.SeedTenantWithClientAsync($"proc-{Guid.NewGuid():N}", Token);
        var exerciseId = await SeedExerciseAsync(owner.TrainerId);
        var video = await SeedVideoAsync(exerciseId, owner.TrainerId, ready: false);
        var lease = await SeedClaimedJobAsync(intruder.TrainerId, video.Id, TimeSpan.FromMinutes(1));

        var status = await TerminateAsync(intruder.TrainerId, lease with { OwnerTrainerId = intruder.TrainerId });

        Assert.Equal(ExerciseVideoJobTransitionStatus.NotFound, status);
        Assert.Equal("processing", await StatusAsync(video.Id));
    }

    private async Task<ExerciseVideoJobTransitionStatus> MarkReadyAsync(Guid? trainerId, ExerciseVideoLease lease)
    {
        await using var context = _fixture.CreateContext(trainerId);
        return await new ExerciseVideoProcessingStore(context, new TestClock(Now)).MarkReadyAsync(
            lease,
            new VideoTechnicalMetadata(VideoContainer.Mp4, 20_000, 1280, 720, "avc1", "mp4a", 1, 1),
            Token);
    }

    private async Task<ExerciseVideoJobTransitionStatus> TerminateAsync(Guid? trainerId, ExerciseVideoLease lease)
    {
        await using var context = _fixture.CreateContext(trainerId);
        return await new ExerciseVideoProcessingStore(context, new TestClock(Now)).TerminateAsync(
            lease, ExerciseVideoTermination.Rejected, "exercise_video_codec_unsupported", Token);
    }

    private async Task<Guid> SeedExerciseAsync(Guid trainerId)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var exercise = IntegrationTestData.Exercise(trainerId, Now);
        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(Token);
        return exercise.Id;
    }

    private async Task<Guid> SeedGlobalExerciseAsync()
    {
        var exercise = IntegrationTestData.Exercise(null, Now);
        var actorUserId = Guid.NewGuid();
        await using var context = _fixture.CreateAdministrativeContext(actorUserId);
        context.Exercises.Add(exercise);
        // O interceptor exige a auditoria com o mesmo ator do contexto administrativo.
        context.AdministrativeAuditEntries.Add(new Domain.Entities.Administration.AdministrativeAuditEntry(
            actorUserId, "create", "exercise", exercise.Id, null, "{}", Now));
        await context.SaveChangesAsync(Token);
        return exercise.Id;
    }

    private async Task<ExerciseVideo> SeedVideoAsync(Guid exerciseId, Guid? owner, bool ready)
    {
        var video = new ExerciseVideo(exerciseId, owner, "video/mp4", 1024, Guid.NewGuid(), Now.AddMinutes(15), Now);
        video.MarkUploaded(1024, "etag-1", Now);
        if (ready)
            video.MarkReady(10_000, 1280, 720, "avc1", null, Now);

        await using var context = owner.HasValue
            ? _fixture.CreateContext(owner)
            : _fixture.CreateAdministrativeContext();
        context.ExerciseVideos.Add(video);
        await context.SaveChangesAsync(Token);
        return video;
    }

    private async Task<ExerciseVideoLease> SeedClaimedJobAsync(Guid? trainerId, Guid videoId, TimeSpan leaseRemaining)
    {
        var job = new DurableJob(
            trainerId,
            ExerciseVideoJobs.ProcessType,
            ExerciseVideoJobs.Version,
            ExerciseVideoJobs.SerializeVideoPayload(videoId),
            $"{ExerciseVideoJobs.ProcessType}:test:{Guid.NewGuid():N}",
            Guid.NewGuid(),
            Now,
            Now);

        await using (var context = _fixture.CreateContext((Guid?)null))
        {
            context.DurableJobs.Add(job);
            await context.SaveChangesAsync(Token);
        }

        var leaseOwnerId = Guid.NewGuid();
        await _fixture.ExecuteSqlAsync(
            "UPDATE durable_jobs SET status = 'processing', attempts = 1, lease_owner_id = @owner, lease_expires_at = @expires WHERE id = @id",
            Token,
            new NpgsqlParameter("owner", leaseOwnerId),
            new NpgsqlParameter("expires", Now.Add(leaseRemaining)),
            new NpgsqlParameter("id", job.Id));

        return new ExerciseVideoLease(videoId, trainerId, job.Id, leaseOwnerId, Guid.NewGuid());
    }

    private async Task<string> StatusAsync(Guid videoId) =>
        (await _fixture.QueryScalarAsync<string>(
            "SELECT status FROM exercise_videos WHERE id = @id", Token, new NpgsqlParameter("id", videoId)))!;

    private Task<long> CountJobsAsync(string idempotencyKey) =>
        _fixture.QueryScalarAsync<long>(
            "SELECT count(*) FROM durable_jobs WHERE idempotency_key = @key", Token, new NpgsqlParameter("key", idempotencyKey));
}
