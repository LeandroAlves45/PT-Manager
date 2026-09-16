using Application.Features.ClientPortal;
using Application.Features.Training;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;

namespace Application.UnitTests.Features.Training.ExerciseVideos;

/// <summary>
/// Prova que só um vídeo Ready visível à audiência recebe URL assinada, com a
/// validade aprovada, e que o bloqueio da plataforma não é revelado.
/// </summary>
public sealed class GetExerciseVideoPlaybackHandlerTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_WhenNoReadyVideoIsVisible_ReturnsNotFoundWithoutSigning()
    {
        var (handler, queries, storage) = Create(VideoTenantContext.Trainer(TrainerId));

        var result = await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Trainer), Token);

        Assert.Equal(ExerciseVideoErrors.VideoNotFound.Code, result.Error!.Code);
        Assert.Empty(storage.Calls);
        Assert.Equal(TrainerId, queries.LastQuery!.Value.TrainerId);
    }

    [Fact]
    public async Task Handle_IssuesAThirtyMinuteUrlForTheCandidateOwner()
    {
        var (handler, queries, storage) = Create(VideoTenantContext.Trainer(TrainerId));
        queries.Candidate = Candidate(TrainerId, blocked: false);

        var result = await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Trainer), Token);

        var expiresAt = VideoTestData.Now.AddMinutes(30);
        Assert.Equal(expiresAt, result.Value.PlaybackExpiresAt);
        Assert.Equal((queries.Candidate.ObjectKey, (Guid?)TrainerId, expiresAt), storage.LastPlayback!.Value);
    }

    [Theory]
    [InlineData(ExerciseVideoPlaybackAudience.Trainer)]
    [InlineData(ExerciseVideoPlaybackAudience.Client)]
    [InlineData(ExerciseVideoPlaybackAudience.GlobalCatalog)]
    public async Task Handle_WhenExerciseIsBlocked_HidesTheVideoOutsideAdministration(
        ExerciseVideoPlaybackAudience audience)
    {
        var context = audience switch
        {
            ExerciseVideoPlaybackAudience.Trainer => VideoTenantContext.Trainer(TrainerId),
            ExerciseVideoPlaybackAudience.Client => VideoTenantContext.Client(TrainerId),
            _ => VideoTenantContext.Administrator()
        };
        var (handler, queries, storage) = Create(context);
        queries.Candidate = Candidate(TrainerId, blocked: true);

        var result = await handler.HandleAsync(Query(audience), Token);

        Assert.Equal(ExerciseVideoErrors.VideoNotFound.Code, result.Error!.Code);
        Assert.Empty(storage.Calls);
    }

    [Fact]
    public async Task Handle_ForAdministrativeAudience_StillPlaysABlockedExercise()
    {
        var (handler, queries, _) = Create(VideoTenantContext.Administrator());
        queries.Candidate = Candidate(TrainerId, blocked: true);

        var result = await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Administrative), Token);

        Assert.True(result.IsSuccess);
        Assert.Null(queries.LastQuery!.Value.TrainerId);
    }

    [Fact]
    public async Task Handle_ForClient_PassesTheAuthenticatedIdentityOnly()
    {
        var context = VideoTenantContext.Client(TrainerId);
        var (handler, queries, _) = Create(context);

        await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Client), Token);

        Assert.Equal((TrainerId, context.UserId), (queries.LastQuery!.Value.TrainerId!.Value, queries.LastQuery.Value.ClientUserId));
    }

    [Fact]
    public async Task Handle_WhenTrainerUsesThePortalAudience_ReturnsClientOnly()
    {
        var (handler, queries, _) = Create(VideoTenantContext.Trainer(TrainerId));

        var result = await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Client), Token);

        Assert.Equal(ClientPortalErrors.ClientOnly.Code, result.Error!.Code);
        Assert.Null(queries.LastQuery);
    }

    [Fact]
    public async Task Handle_WhenClientUsesTheAdministrativeAudience_IsForbidden()
    {
        var (handler, queries, _) = Create(VideoTenantContext.Client(TrainerId));

        var result = await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Administrative), Token);

        Assert.Equal(TrainingErrors.AdministratorOnly.Code, result.Error!.Code);
        Assert.Null(queries.LastQuery);
    }

    [Fact]
    public async Task Handle_WhenStorageIsDisabled_ReturnsDependencyFailure()
    {
        var (handler, queries, storage) = Create(VideoTenantContext.Trainer(TrainerId));
        queries.Candidate = Candidate(TrainerId, blocked: false);
        storage.PlaybackStatus = VideoStorageStatus.Disabled;

        var result = await handler.HandleAsync(Query(ExerciseVideoPlaybackAudience.Trainer), Token);

        Assert.Equal(ExerciseVideoErrors.StorageUnavailable.Code, result.Error!.Code);
    }

    private static GetExerciseVideoPlaybackQuery Query(ExerciseVideoPlaybackAudience audience) =>
        new(audience, Guid.NewGuid());

    private static ExerciseVideoPlaybackCandidate Candidate(Guid? owner, bool blocked)
    {
        var videoId = Guid.NewGuid();
        return new ExerciseVideoPlaybackCandidate(
            videoId,
            Guid.NewGuid(),
            owner,
            Domain.Entities.Training.ExerciseVideo.BuildObjectKey(owner, videoId),
            "video/mp4",
            30_000,
            1280,
            720,
            blocked);
    }

    private static (GetExerciseVideoPlaybackHandler, FakeExerciseVideoQueries, FakeVideoStorage) Create(
        VideoTenantContext context)
    {
        var queries = new FakeExerciseVideoQueries();
        var storage = new FakeVideoStorage();
        return (new GetExerciseVideoPlaybackHandler(
            context,
            new VideoClock(VideoTestData.Now),
            VideoTestData.Settings(),
            queries,
            storage), queries, storage);
    }
}
