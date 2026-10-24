using Application.Features.Training.ExerciseVideos;
using Domain.Entities.Jobs;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Infrastructure.IntegrationTests.Migrations;

/// <summary>
/// Prova que a migration dos vídeos geridos aplica, reverte e reaplica em
/// PostgreSQL 17, e que o preflight manual recusa um rollback que esqueceria
/// objetos privados no R2 ou deixaria jobs de vídeo sem handler.
/// </summary>
[Collection(MigrationLifecycleCollection.Name)]
public sealed class AddExerciseVideosMigrationTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly MigrationLifecycleFixture _fixture;

    public AddExerciseVideosMigrationTests(MigrationLifecycleFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_FromManagedImages_CreatesTableAndRollbackReapplies()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, Token);

        await migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token);
        Assert.True(await TableExistsAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, Token);
        Assert.False(await TableExistsAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token);
        Assert.True(await TableExistsAsync());
        Assert.Equal(
            PostgresContainerFixture.AddExerciseVideosMigration,
            (await context.Database.GetAppliedMigrationsAsync(Token)).Last());
    }

    [Fact]
    public async Task Rollback_WithAReadyVideo_IsBlockedByPreflight()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token);

        // O exercício entra por SQL, só com as colunas que existiam nesta migration: o modelo EF
        // atual tem colunas mais recentes e a escrita falharia num schema antigo.
        var exerciseId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO exercises (id, name, is_active, platform_enforcement_status, created_at, updated_at)
            VALUES ({exerciseId}, 'Rollback squat', true, 'allowed', {Now}, {Now})
            """, Token);
        var video = new ExerciseVideo(exerciseId, null, "video/mp4", 1024, Guid.NewGuid(), Now.AddMinutes(15), Now);
        video.MarkUploaded(1024, "etag", Now);
        video.MarkReady(1000, 1280, 720, "avc1", null, Now);
        context.ExerciseVideos.Add(video);
        await context.SaveChangesAsync(Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, Token));

        Assert.Contains("Rollback blocked", exception.MessageText, StringComparison.Ordinal);
        Assert.True(await TableExistsAsync());
    }

    [Fact]
    public async Task Rollback_WithAPendingVideoJob_IsBlockedByPreflight()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token);

        var videoId = Guid.NewGuid();
        context.DurableJobs.Add(new DurableJob(
            null,
            ExerciseVideoJobs.DeleteObjectType,
            ExerciseVideoJobs.Version,
            ExerciseVideoJobs.SerializeDeleteObjectPayload(videoId, ExerciseVideo.BuildObjectKey(null, videoId)),
            ExerciseVideoJobs.DeleteObjectIdempotencyKey(videoId),
            Guid.NewGuid(),
            Now,
            Now));
        await context.SaveChangesAsync(Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, Token));

        Assert.Contains("durable jobs are still pending", exception.MessageText, StringComparison.Ordinal);
    }

    private async Task<bool> TableExistsAsync() =>
        await _fixture.QueryScalarAsync<bool>(
            "SELECT to_regclass('public.exercise_videos') IS NOT NULL", Token);
}
