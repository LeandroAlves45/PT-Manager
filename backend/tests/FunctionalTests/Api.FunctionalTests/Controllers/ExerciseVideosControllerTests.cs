using System.Net;
using System.Text;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova a superfície HTTP dos vídeos geridos contra PostgreSQL real. Só o storage
/// R2 e o probe de container, as dependências externas, são substituídos.
/// </summary>
/// <remarks>
/// Cobre o Gate 5D na fronteira HTTP: lifecycle de upload, ownership e acesso
/// cross-tenant recusados, leitura por audiência, publicação só pelo dispatcher e
/// storage desligado por omissão.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class ExerciseVideosControllerTests : IDisposable
{
    private readonly RecordingVideoStorage _storage = new();
    private readonly ApiWebApplicationFactory _factory;
    private readonly ApiWebApplicationFactory _defaultFactory;

    public ExerciseVideosControllerTests(PostgresApiFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _defaultFactory = fixture.Factory;
        _factory = new ApiWebApplicationFactory(fixture.ConnectionString)
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IVideoObjectStorage>();
                services.RemoveAll<IVideoMetadataProbe>();
                services.AddSingleton<IVideoObjectStorage>(_storage);
                services.AddSingleton<IVideoMetadataProbe>(new AcceptingProbe());
            }
        };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RequestUpload_WithTheDefaultConfiguration_IsUnavailableAndReservesNothing()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_defaultFactory, $"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await TrainingTestData.SeedPrivateExerciseAsync(_defaultFactory, trainer.TrainerId, "Squat", Token);

        var response = await _defaultFactory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId))
            .PostAsync($"/api/v1/exercises/{exerciseId}/video/uploads", UploadBody(), Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, await CountVideosAsync(exerciseId));
    }

    [Fact]
    public async Task TrainerLifecycle_RegistersCompletesAndSchedulesProcessingWithoutExposingTheObjectKey()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await TrainingTestData.SeedPrivateExerciseAsync(_factory, trainer.TrainerId, "Squat", Token);
        var client = TrainerClient(trainer.TrainerId);

        var created = await client.PostAsync($"/api/v1/exercises/{exerciseId}/video/uploads", UploadBody(), Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadJsonAsync(created);
        var videoId = body.GetProperty("video").GetProperty("id").GetGuid();
        Assert.Equal("pending", body.GetProperty("video").GetProperty("status").GetString());
        Assert.Equal(("PUT", "video/mp4"), (
            body.GetProperty("upload").GetProperty("method").GetString(),
            body.GetProperty("upload").GetProperty("content_type").GetString()));
        Assert.DoesNotContain("object_key", await created.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal($"/api/v1/exercises/{exerciseId}/video/uploads/{videoId}", created.Headers.Location!.OriginalString);

        _storage.StoredSizeBytes = 1024;
        var completed = await client.PostAsync(
            $"/api/v1/exercises/{exerciseId}/video/uploads/{videoId}/complete", null, Token);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("processing", (await ReadJsonAsync(completed)).GetProperty("status").GetString());
        Assert.Equal(1, await CountJobsAsync(ExerciseVideoJobs.ProcessIdempotencyKey(videoId)));
        Assert.Contains(_storage.HeadOwners, owner => owner == trainer.TrainerId);
    }

    [Fact]
    public async Task Complete_WhenStoredSizeDiffersFromTheAuthorization_RejectsTheUpload()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await TrainingTestData.SeedPrivateExerciseAsync(_factory, trainer.TrainerId, "Squat", Token);
        var client = TrainerClient(trainer.TrainerId);
        var videoId = await RequestUploadAsync(client, $"/api/v1/exercises/{exerciseId}");

        _storage.StoredSizeBytes = 5_000_000;
        var completed = await client.PostAsync(
            $"/api/v1/exercises/{exerciseId}/video/uploads/{videoId}/complete", null, Token);

        Assert.Equal(HttpStatusCode.Conflict, completed.StatusCode);
        Assert.Contains("exercise_video_upload_rejected", await completed.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(1, await CountJobsAsync(ExerciseVideoJobs.DeleteObjectIdempotencyKey(videoId)));
    }

    [Fact]
    public async Task AnotherTrainer_CannotUploadToOrReadAnExerciseTheyDoNotOwn()
    {
        var owner = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"video-{Guid.NewGuid():N}", Token);
        var intruder = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await TrainingTestData.SeedPrivateExerciseAsync(_factory, owner.TrainerId, "Squat", Token);
        var videoId = await RequestUploadAsync(TrainerClient(owner.TrainerId), $"/api/v1/exercises/{exerciseId}");
        var intruderClient = TrainerClient(intruder.TrainerId);

        var upload = await intruderClient.PostAsync($"/api/v1/exercises/{exerciseId}/video/uploads", UploadBody(), Token);
        var status = await intruderClient.GetAsync($"/api/v1/exercises/{exerciseId}/video/uploads/{videoId}", Token);
        var complete = await intruderClient.PostAsync(
            $"/api/v1/exercises/{exerciseId}/video/uploads/{videoId}/complete", null, Token);

        Assert.Equal(
            (HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound),
            (upload.StatusCode, status.StatusCode, complete.StatusCode));
        Assert.Equal(1, await CountVideosAsync(exerciseId));
    }

    [Fact]
    public async Task GlobalLifecycle_IsPublishedOnlyByTheDispatcherAndVisibleToTrainers()
    {
        var superuserId = await TrainingTestData.SeedSuperuserAsync(_factory, Token);
        var exerciseId = await TrainingTestData.SeedGlobalExerciseAsync(_factory, $"Global {Guid.NewGuid():N}", Token);
        var superuser = _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueSuperuser(superuserId));
        var videoId = await RequestUploadAsync(superuser, $"/api/v1/global-exercises/{exerciseId}");
        _storage.StoredSizeBytes = 1024;
        (await superuser.PostAsync(
            $"/api/v1/global-exercises/{exerciseId}/video/uploads/{videoId}/complete", null, Token)).EnsureSuccessStatusCode();

        var beforeDispatch = await superuser.GetAsync($"/api/v1/global-exercises/{exerciseId}/video", Token);
        Assert.Equal(HttpStatusCode.NotFound, beforeDispatch.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IJobDispatchActivation>().ActivateAsync(Token);

        var playback = await superuser.GetAsync($"/api/v1/global-exercises/{exerciseId}/video", Token);
        Assert.Equal(HttpStatusCode.OK, playback.StatusCode);
        Assert.Equal(RecordingVideoStorage.PlaybackUrl, (await ReadJsonAsync(playback)).GetProperty("playback_url").GetString());

        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"video-{Guid.NewGuid():N}", Token);
        var trainerPlayback = await TrainerClient(trainer.TrainerId).GetAsync($"/api/v1/exercises/{exerciseId}/video", Token);
        Assert.Equal(HttpStatusCode.OK, trainerPlayback.StatusCode);
        Assert.Contains(_storage.PlaybackOwners, owner => owner is null);
    }

    [Fact]
    public async Task ClientPlayback_IsLimitedToExercisesOfTheActivePlan()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token);
        var planExerciseId = await FindPlanExerciseAsync(trainerId, clientUserId);
        var otherExerciseId = await TrainingTestData.SeedPrivateExerciseAsync(_factory, trainerId, "Not in plan", Token);
        await SeedReadyVideoAsync(trainerId, planExerciseId);
        await SeedReadyVideoAsync(trainerId, otherExerciseId);
        var client = _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var inPlan = await client.GetAsync($"/api/v1/portal/my-plan/exercises/{planExerciseId}/video", Token);
        var notInPlan = await client.GetAsync($"/api/v1/portal/my-plan/exercises/{otherExerciseId}/video", Token);

        var (otherTrainerId, otherClientUserId) = await PortalTestData.SeedActiveClientAsync(_factory, Token);
        var foreignClient = await _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(otherClientUserId, otherTrainerId))
            .GetAsync($"/api/v1/portal/my-plan/exercises/{planExerciseId}/video", Token);

        Assert.Equal(
            (HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound),
            (inPlan.StatusCode, notInPlan.StatusCode, foreignClient.StatusCode));
        Assert.DoesNotContain("video_codec", await inPlan.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministrativeRoute_PlaysPrivateVideosButRejectsTrainers()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, $"video-{Guid.NewGuid():N}", Token);
        var exerciseId = await TrainingTestData.SeedPrivateExerciseAsync(_factory, trainer.TrainerId, "Squat", Token);
        await SeedReadyVideoAsync(trainer.TrainerId, exerciseId);
        var superuserId = await TrainingTestData.SeedSuperuserAsync(_factory, Token);
        var route = $"/api/v1/admin/content-moderation/exercises/{exerciseId}/video";

        var administrative = await _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(superuserId)).GetAsync(route, Token);
        var trainerAttempt = await TrainerClient(trainer.TrainerId).GetAsync(route, Token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden), (administrative.StatusCode, trainerAttempt.StatusCode));
    }

    [Fact]
    public async Task DeleteGlobalExercise_WithAReadyVideo_ReturnsConflict()
    {
        var superuserId = await TrainingTestData.SeedSuperuserAsync(_factory, Token);
        var exerciseId = await TrainingTestData.SeedGlobalExerciseAsync(_factory, $"Global {Guid.NewGuid():N}", Token);
        await SeedReadyVideoAsync(null, exerciseId, superuserId);

        var response = await _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(superuserId))
            .DeleteAsync($"/api/v1/global-exercises/{exerciseId}", Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("global_exercise_has_video", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static StringContent UploadBody(long sizeBytes = 1024) =>
        new($$"""{"content_type":"video/mp4","size_bytes":{{sizeBytes}}}""", Encoding.UTF8, "application/json");

    private static async Task<Guid> RequestUploadAsync(HttpClient client, string exerciseRoute)
    {
        var response = await client.PostAsync($"{exerciseRoute}/video/uploads", UploadBody(), Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("video").GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return document.RootElement.Clone();
    }

    private async Task SeedReadyVideoAsync(Guid? trainerId, Guid exerciseId, Guid? superuserId = null)
    {
        var now = DateTime.UtcNow;
        var video = new ExerciseVideo(
            exerciseId, trainerId, "video/mp4", 1024, superuserId ?? trainerId!.Value, now.AddMinutes(15), now);
        video.MarkUploaded(1024, "etag-ready", now);
        video.MarkReady(20_000, 1280, 720, "avc1", "mp4a", now);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Establish(
            trainerId,
            trainerId ?? superuserId,
            trainerId.HasValue ? "trainer" : "superuser",
            TenantOrigin.Http,
            isAdministrative: !trainerId.HasValue);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        context.ExerciseVideos.Add(video);
        await context.SaveChangesAsync(Token);
    }

    private async Task<Guid> FindPlanExerciseAsync(Guid trainerId, Guid clientUserId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        return await (
            from client in context.Clients.IgnoreQueryFilters()
            join plan in context.TrainingPlans.IgnoreQueryFilters() on client.Id equals plan.ClientId
            join day in context.TrainingPlanDays.IgnoreQueryFilters() on plan.Id equals day.TrainingPlanId
            join item in context.TrainingPlanDayExercises.IgnoreQueryFilters() on day.Id equals item.TrainingPlanDayId
            where client.OwnerTrainerId == trainerId && client.UserId == clientUserId && plan.IsActive
            select item.ExerciseId).FirstAsync(Token);
    }

    private async Task<int> CountVideosAsync(Guid exerciseId)
    {
        await using var scope = _defaultFactory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>()
            .ExerciseVideos.IgnoreQueryFilters().CountAsync(video => video.ExerciseId == exerciseId, Token);
    }

    private async Task<int> CountJobsAsync(string idempotencyKey)
    {
        await using var scope = _defaultFactory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>()
            .DurableJobs.CountAsync(job => job.IdempotencyKey == idempotencyKey, Token);
    }

    private sealed class RecordingVideoStorage : IVideoObjectStorage
    {
        public const string PlaybackUrl = "https://r2.test/playback";

        public long StoredSizeBytes { get; set; } = 1024;
        public List<Guid?> HeadOwners { get; } = [];
        public List<Guid?> PlaybackOwners { get; } = [];

        public Task<VideoUploadAuthorizationOutcome> CreateUploadAuthorizationAsync(
            string objectKey, Guid? ownerTrainerId, string contentType, long contentLength, DateTime expiresAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(new VideoUploadAuthorizationOutcome(
                VideoStorageStatus.Success,
                new VideoUploadAuthorization(new Uri("https://r2.test/upload"), "PUT", contentType, contentLength, expiresAt)));

        public Task<VideoObjectInfoOutcome> GetObjectInfoAsync(
            string objectKey, Guid? ownerTrainerId, CancellationToken cancellationToken)
        {
            HeadOwners.Add(ownerTrainerId);
            return Task.FromResult(new VideoObjectInfoOutcome(
                VideoStorageStatus.Success, new VideoObjectInfo(StoredSizeBytes, "etag-stored", "video/mp4")));
        }

        public Task<VideoPlaybackUrlOutcome> CreatePlaybackUrlAsync(
            string objectKey, Guid? ownerTrainerId, DateTime expiresAt, CancellationToken cancellationToken)
        {
            PlaybackOwners.Add(ownerTrainerId);
            return Task.FromResult(new VideoPlaybackUrlOutcome(
                VideoStorageStatus.Success, new VideoPlaybackUrl(new Uri(PlaybackUrl), expiresAt)));
        }

        public Task<VideoObjectDeletionOutcome> DeleteAsync(
            string objectKey, Guid? ownerTrainerId, CancellationToken cancellationToken) =>
            Task.FromResult(new VideoObjectDeletionOutcome(VideoStorageStatus.Success));
    }

    private sealed class AcceptingProbe : IVideoMetadataProbe
    {
        public Task<VideoProbeOutcome> ProbeAsync(
            string objectKey, Guid? ownerTrainerId, long sizeBytes, string eTag, CancellationToken cancellationToken) =>
            Task.FromResult(VideoProbeOutcome.Probed(
                new VideoTechnicalMetadata(VideoContainer.Mp4, 20_000, 1280, 720, "avc1", "mp4a", 1, 1)));
    }
}
