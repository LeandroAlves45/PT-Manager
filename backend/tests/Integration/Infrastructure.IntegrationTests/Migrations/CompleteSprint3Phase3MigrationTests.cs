using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

[Collection(MigrationLifecycleCollection.Name)]
public sealed class CompleteSprint3Phase3MigrationTests : IAsyncLifetime
{
    private const string TrainerId = "10000000-0000-0000-0000-000000000001";
    private const string ClientId = "20000000-0000-0000-0000-000000000001";
    private const string SupplementId = "30000000-0000-0000-0000-000000000001";
    private const string PackTypeId = "40000000-0000-0000-0000-000000000001";
    private readonly MigrationLifecycleFixture _fixture;

    public CompleteSprint3Phase3MigrationTests(MigrationLifecycleFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_WhenDatabaseIsEmpty_AppliesPhase3Schema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();

        // O alvo explícito mantém este teste histórico estável quando surgem migrations novas.
        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Phase3Migration,
            cancellationToken);

        var tableCount = await _fixture.CountApplicationTablesAsync(cancellationToken);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

        Assert.Equal(29, tableCount);
        Assert.Equal(PostgresContainerFixture.CompleteSprint3Phase3Migration, applied.Last());
        Assert.True(await _fixture.TableExistsAsync(
            "administrative_audit_entries",
            cancellationToken));
    }

    [Fact]
    public async Task Migrate_WhenLegacyDataIsValid_PreservesMeaningAndBackfillsNewState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await MigrateToPreviousAsync(cancellationToken);
        await _fixture.ExecuteSqlAsync(ValidLegacyDataSql, cancellationToken);

        await using var context = _fixture.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);

        var preserved = await _fixture.QueryScalarAsync<bool>(
            """
            SELECT
                (SELECT NOT is_active FROM foods WHERE id = '51000000-0000-0000-0000-000000000001')
                AND (SELECT NOT is_active FROM exercises WHERE id = '52000000-0000-0000-0000-000000000001')
                AND (SELECT NOT is_active FROM supplements WHERE id = '30000000-0000-0000-0000-000000000001')
                AND (SELECT created_by_user_id = owner_trainer_id FROM supplements WHERE id = '30000000-0000-0000-0000-000000000001')
                AND (SELECT NOT is_active FROM client_supplement_assignments WHERE id = '53000000-0000-0000-0000-000000000001')
                AND (SELECT completed_at = updated_at FROM client_session_packs WHERE id = '54000000-0000-0000-0000-000000000001')
                AND (SELECT responded_at = updated_at FROM checkins WHERE id = '55000000-0000-0000-0000-000000000001');
            """,
            cancellationToken);

        Assert.True(preserved);
    }

    [Theory]
    [MemberData(nameof(InvalidLegacyScenarios))]
    public async Task Migrate_WhenPreflightDataIsInvalid_AbortsWithoutApplyingMigration(
        string invalidDataSql,
        string expectedMessage)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await MigrateToPreviousAsync(cancellationToken);
        await _fixture.ExecuteSqlAsync(BaseLegacyDataSql, cancellationToken);
        await _fixture.ExecuteSqlAsync(invalidDataSql, cancellationToken);

        await using var context = _fixture.CreateContext();
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => context.Database.MigrateAsync(cancellationToken));
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

        Assert.Contains(expectedMessage, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(PostgresContainerFixture.CompleteSprint3Phase3Migration, applied);
        Assert.False(await _fixture.TableExistsAsync(
            "administrative_audit_entries",
            cancellationToken));
    }

    [Fact]
    public async Task Migrate_WhenLatestWasRolledBack_CanApplyAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Phase3Migration,
            cancellationToken);

        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteTrainingPhase2CMigration,
            cancellationToken);
        Assert.False(await _fixture.TableExistsAsync(
            "administrative_audit_entries",
            cancellationToken));

        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Phase3Migration,
            cancellationToken);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

        Assert.Equal(PostgresContainerFixture.CompleteSprint3Phase3Migration, applied.Last());
        Assert.True(await _fixture.TableExistsAsync(
            "administrative_audit_entries",
            cancellationToken));
    }

    public static TheoryData<string, string> InvalidLegacyScenarios => new()
    {
        {
            $"UPDATE trainer_settings SET app_name = repeat('x', 51) WHERE trainer_id = '{TrainerId}';",
            "trainer_settings.app_name exceeds 50 characters"
        },
        {
            """
            INSERT INTO supplements
                (id, owner_trainer_id, created_by_user_id, name, unit_of_measure, serving_size, timing)
            VALUES
                ('31000000-0000-0000-0000-000000000001', NULL, NULL, 'Global', 'g', '5 g', 'daily');
            """,
            "a global supplement has no explicit author"
        },
        {
            """
            INSERT INTO users
                (id, email, normalized_email, password_hash, security_stamp, concurrency_stamp, role, created_at, updated_at)
            VALUES
                ('00000000-0000-0000-0000-000000000000', 'empty@example.test', 'EMPTY@EXAMPLE.TEST', 'hash', 'security', 'concurrency', 'trainer', now(), now());
            INSERT INTO supplements
                (id, owner_trainer_id, created_by_user_id, name, unit_of_measure, serving_size, timing)
            VALUES
                ('31000000-0000-0000-0000-000000000002', NULL, '00000000-0000-0000-0000-000000000000', 'Invalid author', 'g', '5 g', 'daily');
            """,
            "a supplement uses an empty author UUID"
        },
        {
            $"""
            INSERT INTO supplements
                (id, owner_trainer_id, created_by_user_id, name, unit_of_measure, serving_size, timing)
            VALUES
                ('31000000-0000-0000-0000-000000000003', '{TrainerId}', '{TrainerId}', '   ', 'g', '5 g', 'daily');
            """,
            "a required supplement field is blank"
        },
        {
            $$"""
            INSERT INTO checkins
                (id, owner_trainer_id, client_id, check_in_date, weight_kg, body_fat_percentage, body_measurements, feedback)
            VALUES
                ('55000000-0000-0000-0000-000000000002', '{{TrainerId}}', '{{ClientId}}', DATE '2026-08-22', 80, 0, '{}', '{}');
            """,
            "a check-in has body fat equal to 0 or 100"
        },
        {
            $"""
            INSERT INTO sessions
                (id, owner_trainer_id, client_id, starts_at, duration_minutes, status, status_changed_at, created_at, updated_at)
            VALUES
                ('56000000-0000-0000-0000-000000000001', '{TrainerId}', '{ClientId}', TIMESTAMPTZ '2026-09-01 10:00:00+00', 60, 'scheduled', now(), now(), now()),
                ('56000000-0000-0000-0000-000000000002', '{TrainerId}', '{ClientId}', TIMESTAMPTZ '2026-09-01 10:00:00+00', 60, 'scheduled', now(), now(), now());
            """,
            "duplicate scheduled sessions exist"
        },
        {
            $"""
            ALTER TABLE pack_types DROP CONSTRAINT pack_session_count_positive;
            INSERT INTO pack_types
                (id, owner_trainer_id, name, session_count, price_cents)
            VALUES
                ('41000000-0000-0000-0000-000000000001', '{TrainerId}', 'Invalid pack type', 0, 1000);
            """,
            "a pack type violates the target constraints"
        },
        {
            $"""
            INSERT INTO pack_types
                (id, owner_trainer_id, name, session_count, price_cents)
            VALUES
                ('{PackTypeId}', '{TrainerId}', 'Valid pack type', 10, 1000);
            ALTER TABLE client_session_packs DROP CONSTRAINT pack_sessions_consistent;
            INSERT INTO client_session_packs
                (id, owner_trainer_id, client_id, pack_type_id, pack_name, total_sessions, sessions_remaining, price_cents, currency, purchase_date)
            VALUES
                ('54000000-0000-0000-0000-000000000002', '{TrainerId}', '{ClientId}', '{PackTypeId}', 'Invalid client pack', 0, 0, 1000, 'EUR', DATE '2026-08-01');
            """,
            "a client session pack violates the target constraints"
        }
    };

    private async Task MigrateToPreviousAsync(CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteTrainingPhase2CMigration,
            cancellationToken);
    }

    private const string BaseLegacyDataSql = $$"""
        INSERT INTO users
            (id, email, normalized_email, password_hash, security_stamp, concurrency_stamp, role, created_at, updated_at)
        VALUES
            ('{{TrainerId}}', 'trainer@example.test', 'TRAINER@EXAMPLE.TEST', 'hash', 'security', 'concurrency', 'trainer', now(), now());

        INSERT INTO clients
            (id, owner_trainer_id, name, phone, date_of_birth, sex)
        VALUES
            ('{{ClientId}}', '{{TrainerId}}', 'Legacy Client', '+351900000000', DATE '1990-01-01', 'male');

        INSERT INTO trainer_settings (id, trainer_id)
        VALUES ('60000000-0000-0000-0000-000000000001', '{{TrainerId}}');
        """;

    private const string ValidLegacyDataSql = BaseLegacyDataSql + $$"""

        INSERT INTO foods
            (id, owner_trainer_id, name, protein, carbs, fats, is_deleted)
        VALUES
            ('51000000-0000-0000-0000-000000000001', '{{TrainerId}}', 'Archived food', 10, 20, 5, true);

        INSERT INTO exercises (id, owner_trainer_id, name, is_deleted)
        VALUES ('52000000-0000-0000-0000-000000000001', '{{TrainerId}}', 'Archived exercise', true);

        INSERT INTO supplements
            (id, owner_trainer_id, created_by_user_id, name, unit_of_measure, serving_size, timing, is_deleted)
        VALUES
            ('{{SupplementId}}', '{{TrainerId}}', NULL, 'Archived supplement', 'g', '5 g', 'daily', true);

        INSERT INTO client_supplement_assignments
            (id, owner_trainer_id, client_id, supplement_id, serving_size, timing, is_deleted)
        VALUES
            ('53000000-0000-0000-0000-000000000001', '{{TrainerId}}', '{{ClientId}}', '{{SupplementId}}', '5 g', 'daily', true);

        INSERT INTO pack_types
            (id, owner_trainer_id, name, session_count, price_cents)
        VALUES
            ('{{PackTypeId}}', '{{TrainerId}}', 'Legacy pack type', 10, 10000);

        INSERT INTO client_session_packs
            (id, owner_trainer_id, client_id, pack_type_id, pack_name, total_sessions, sessions_remaining, price_cents, currency, purchase_date, updated_at)
        VALUES
            ('54000000-0000-0000-0000-000000000001', '{{TrainerId}}', '{{ClientId}}', '{{PackTypeId}}', 'Completed pack', 10, 0, 10000, 'EUR', DATE '2026-01-01', TIMESTAMPTZ '2026-08-01 12:00:00+00');

        INSERT INTO checkins
            (id, owner_trainer_id, client_id, check_in_date, weight_kg, body_fat_percentage, body_measurements, feedback, updated_at)
        VALUES
            ('55000000-0000-0000-0000-000000000001', '{{TrainerId}}', '{{ClientId}}', DATE '2026-08-01', 80, 20, '{}', '{}', TIMESTAMPTZ '2026-08-02 12:00:00+00');
        """;
}
