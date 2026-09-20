using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

/// <summary>
/// Prova que a migration de índices da Sprint 6B aplica, reverte e reaplica em PostgreSQL 17.
/// É só aditiva (três índices parciais), pelo que o Down não precisa de preflight: largar um
/// índice nunca apaga dados.
/// </summary>
[Collection(MigrationLifecycleCollection.Name)]
public sealed class AddModerationAndCheckInReadIndexesMigrationTests : IAsyncLifetime
{
    private readonly MigrationLifecycleFixture _fixture;

    public AddModerationAndCheckInReadIndexesMigrationTests(MigrationLifecycleFixture fixture) =>
        _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_FromSprint6A_CreatesIndexesAndRollbackReapplies()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);
        Assert.False(await IndexesExistAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddModerationAndCheckInReadIndexesMigration, Token);
        Assert.True(await IndexesExistAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);
        Assert.False(await IndexesExistAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddModerationAndCheckInReadIndexesMigration, Token);
        Assert.True(await IndexesExistAsync());
    }

    [Fact]
    public async Task ModerationQueueIndexes_ArePartialOnPrivateContent()
    {
        await using var context = _fixture.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(
            PostgresContainerFixture.AddModerationAndCheckInReadIndexesMigration, Token);

        var definition = await _fixture.QueryScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE indexname = 'idx_foods_moderation_queue'",
            Token);

        Assert.Contains("owner_trainer_id IS NOT NULL", definition!, StringComparison.Ordinal);
        Assert.Contains("updated_at DESC", definition!, StringComparison.Ordinal);
    }

    private async Task<bool> IndexesExistAsync() =>
        await _fixture.QueryScalarAsync<bool>(
            """
            SELECT to_regclass('public.idx_foods_moderation_queue') IS NOT NULL
               AND to_regclass('public.idx_exercises_moderation_queue') IS NOT NULL
               AND to_regclass('public.idx_checkins_pending_review') IS NOT NULL
            """,
            Token);
}
