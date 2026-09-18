using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Infrastructure.IntegrationTests.Migrations;

/// <summary>
/// Prova que a migration da Sprint 6A aplica, reverte e reaplica em PostgreSQL 17, que as
/// check constraints do RPE recusam valores fora da escala e que o preflight do Down
/// recusa um rollback que apagaria histórico do cliente.
/// </summary>
[Collection(MigrationLifecycleCollection.Name)]
public sealed class AddSprint6AWriteSchemaMigrationTests : IAsyncLifetime
{
    private readonly MigrationLifecycleFixture _fixture;

    public AddSprint6AWriteSchemaMigrationTests(MigrationLifecycleFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_FromExerciseVideos_CreatesSchemaAndRollbackReapplies()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token);

        await migrator.MigrateAsync(PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);
        Assert.True(await SchemaExistsAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token);
        Assert.False(await SchemaExistsAsync());

        await migrator.MigrateAsync(PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);
        Assert.True(await SchemaExistsAsync());
    }

    [Fact]
    public async Task PlannedRpe_OutsideHalfSteps_IsRejectedByDatabase()
    {
        await using var context = _fixture.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(
            PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);

        // replica desliga os triggers das FK nesta ligação; as check constraints continuam ativas.
        var exception = await Assert.ThrowsAsync<PostgresException>(() => _fixture.ExecuteSqlAsync(
            "SET session_replication_role = replica; " +
            "INSERT INTO exercise_sets (id, training_plan_day_exercise_id, set_number, planned_rpe) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), 1, 7.3);",
            Token));

        Assert.Equal("planned_rpe_check", exception.ConstraintName);
    }

    [Fact]
    public async Task Rollback_WithClientHistory_IsBlockedByPreflight()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);

        // O preflight só verifica a existência de histórico; a linha mínima dispensa o grafo de FK.
        await _fixture.ExecuteSqlAsync(
            "SET session_replication_role = replica; " +
            "INSERT INTO client_supplement_intakes " +
            "(id, owner_trainer_id, client_id, client_supplement_assignment_id, local_date) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), DATE '2026-09-16');",
            Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token));

        Assert.Contains("Rollback blocked", exception.MessageText, StringComparison.Ordinal);
        Assert.True(await SchemaExistsAsync());
    }

    [Fact]
    public async Task Rollback_WithSprint6AColumnData_IsBlockedByPreflight()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddSprint6AWriteSchemaMigration, Token);

        // Segundo ramo do preflight: sem tabelas novas preenchidas, só uma coluna 6A com valor.
        await _fixture.ExecuteSqlAsync(
            "SET session_replication_role = replica; " +
            "INSERT INTO exercise_sets (id, training_plan_day_exercise_id, set_number, planned_rpe) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), 1, 8);",
            Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(PostgresContainerFixture.AddExerciseVideosMigration, Token));

        Assert.Contains("Sprint 6A columns contain data", exception.MessageText, StringComparison.Ordinal);
        Assert.True(await SchemaExistsAsync());
    }

    private async Task<bool> SchemaExistsAsync() =>
        await _fixture.QueryScalarAsync<bool>(
            "SELECT to_regclass('public.workout_completions') IS NOT NULL " +
            "AND to_regclass('public.client_supplement_intakes') IS NOT NULL " +
            "AND EXISTS (SELECT 1 FROM information_schema.columns " +
            "WHERE table_name = 'exercise_sets' AND column_name = 'planned_rpe')",
            Token);
}
