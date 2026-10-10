using Domain.Entities.Administration;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.ClientPortal;

namespace Infrastructure.IntegrationTests.ClientPortal;

/// <summary>
/// Prova, contra PostgreSQL real, que o plano do cliente expõe o id do catálogo e que
/// <c>has_ready_video</c> segue exatamente a regra do pedido de vídeo do cliente: vídeo Ready
/// global ou do próprio trainer, e exercício não bloqueado.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MyTrainingPlanQueriesTests : ReadQueriesIntegrationTestContext
{
    public MyTrainingPlanQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetActive_ExposesCatalogIdAndOnlyPlayableReadyVideos()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var otherTenant = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);

        var platformVideo = await SeedGlobalExerciseAsync(token);
        await SeedVideoAsync(platformVideo, owner: null, ready: true, token);

        var ownVideoOnGlobal = await SeedGlobalExerciseAsync(token);
        await SeedVideoAsync(ownVideoOnGlobal, seed.TrainerId, ready: true, token);

        var foreignVideoOnGlobal = await SeedGlobalExerciseAsync(token);
        await SeedVideoAsync(foreignVideoOnGlobal, otherTenant.TrainerId, ready: true, token);

        var pendingVideo = await SeedPrivateExerciseAsync(seed.TrainerId, token);
        await SeedVideoAsync(pendingVideo, seed.TrainerId, ready: false, token);

        var blockedWithVideo = await SeedPrivateExerciseAsync(seed.TrainerId, token);
        await SeedVideoAsync(blockedWithVideo, seed.TrainerId, ready: true, token);

        var withoutVideo = await SeedPrivateExerciseAsync(seed.TrainerId, token);

        Guid[] catalogIds =
        [
            platformVideo,
            ownVideoOnGlobal,
            foreignVideoOnGlobal,
            pendingVideo,
            blockedWithVideo,
            withoutVideo
        ];

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var plan = new TrainingPlan(
                seed.TrainerId, seed.ClientId, "Força", null, null, null,
                new DateOnly(2026, 8, 31), null, Now);
            var day = plan.AddDay(3, 1, null, Now);
            for (var index = 0; index < catalogIds.Length; index++)
                day.AddExercise(catalogIds[index], index + 1, null, null, null, Now)
                    .AddSet(1, 8, 60m, 60, 90, Now, 8m);

            context.TrainingPlans.Add(plan);
            await context.SaveChangesAsync(token);
        }

        // Bloqueado depois de prescrito: o interceptor recusa referenciar um exercício já
        // bloqueado, por isso este é o único caminho real até um plano com um.
        await BlockExerciseAsync(seed.TrainerId, blockedWithVideo, token);

        await using var readContext = _fixture.CreateContext(seed.TrainerId);
        var myPlan = await new MyTrainingPlanQueries(readContext)
            .GetActiveAsync(seed.TrainerId, seed.ClientUserId, token);

        var exercises = Assert.Single(myPlan!.Days).Exercises;
        Assert.Equal(catalogIds, exercises.Select(exercise => exercise.ExerciseId));
        Assert.All(exercises, exercise => Assert.NotEqual(exercise.ExerciseId, exercise.Id));
        Assert.Equal(
            [true, true, false, false, false, false],
            exercises.Select(exercise => exercise.HasReadyVideo));

        // O exercício bloqueado continua com o id do catálogo (U11), mas mascarado.
        var blocked = exercises[4];
        Assert.True(blocked.IsUnavailable);
        Assert.Equal(blockedWithVideo, blocked.ExerciseId);
    }

    private async Task<Guid> SeedGlobalExerciseAsync(CancellationToken token)
    {
        var exercise = IntegrationTestData.Exercise(null, Now);
        var actorUserId = Guid.NewGuid();
        await using var context = _fixture.CreateAdministrativeContext(actorUserId);
        context.Exercises.Add(exercise);
        // O interceptor exige a auditoria com o mesmo ator do contexto administrativo.
        context.AdministrativeAuditEntries.Add(new AdministrativeAuditEntry(
            actorUserId, "create", "exercise", exercise.Id, null, "{}", Now));
        await context.SaveChangesAsync(token);
        return exercise.Id;
    }

    private async Task<Guid> SeedPrivateExerciseAsync(Guid trainerId, CancellationToken token)
    {
        var exercise = IntegrationTestData.Exercise(trainerId, Now);
        await using var context = _fixture.CreateContext(trainerId);
        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(token);
        return exercise.Id;
    }

    private async Task BlockExerciseAsync(Guid trainerId, Guid exerciseId, CancellationToken token)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var exercise = await context.Exercises.FindAsync([exerciseId], token)
            ?? throw new InvalidOperationException("Exercise seed missing.");
        exercise.Block(PlatformEnforcementReason.MaliciousContent, Now);
        await context.SaveChangesAsync(token);
    }

    private async Task SeedVideoAsync(Guid exerciseId, Guid? owner, bool ready, CancellationToken token)
    {
        var video = new ExerciseVideo(
            exerciseId, owner, "video/mp4", 1024, Guid.NewGuid(), Now.AddMinutes(15), Now);
        video.MarkUploaded(1024, "etag-1", Now);
        if (ready)
            video.MarkReady(10_000, 1280, 720, "avc1", null, Now);

        await using var context = owner.HasValue
            ? _fixture.CreateContext(owner)
            : _fixture.CreateAdministrativeContext();
        context.ExerciseVideos.Add(video);
        await context.SaveChangesAsync(token);
    }
}
