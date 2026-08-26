using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

[Collection(MigrationLifecycleCollection.Name)]
public sealed class CompleteSprint3Lote3GMigrationTests : IAsyncLifetime
{
    private readonly MigrationLifecycleFixture _fixture;

    public CompleteSprint3Lote3GMigrationTests(MigrationLifecycleFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_WhenPhase3SchemaExists_AppliesLote3GSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Phase3Migration,
            cancellationToken);

        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Lote3GMigration,
            cancellationToken);

        var schemaIsComplete = await _fixture.QueryScalarAsync<bool>(
            """
            SELECT
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                        AND table_name = 'trainer_subscriptions'
                        AND column_name = 'last_provider_state_observed_at'
                        AND is_nullable = 'YES'
                        AND data_type = 'timestamp with time zone')
                AND to_regclass('public.email_verification_tokens') IS NOT NULL
                AND to_regclass('public.password_reset_tokens') IS NOT NULL
                AND to_regclass('public.tenant_transfer_audits') IS NOT NULL
                AND to_regclass('public.uq_clients_user_active') IS NOT NULL
                AND to_regclass('public.uq_trainer_subscriptions_stripe_customer') IS NOT NULL
                AND to_regclass('public.uq_trainer_subscriptions_stripe_subscription') IS NOT NULL;
            """,
            cancellationToken);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

        Assert.True(schemaIsComplete);
        Assert.Equal(PostgresContainerFixture.CompleteSprint3Lote3GMigration, applied.Last());
    }

    [Fact]
    public async Task Migrate_WhenLote3GWasRolledBack_CanApplyAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Lote3GMigration,
            cancellationToken);

        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Phase3Migration,
            cancellationToken);
        var rollbackIsComplete = await _fixture.QueryScalarAsync<bool>(
            """
            SELECT
                NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                        AND table_name = 'trainer_subscriptions'
                        AND column_name = 'last_provider_state_observed_at')
                AND to_regclass('public.email_verification_tokens') IS NULL
                AND to_regclass('public.password_reset_tokens') IS NULL
                AND to_regclass('public.tenant_transfer_audits') IS NULL
                AND to_regclass('public.uq_clients_user') IS NOT NULL;
            """,
            cancellationToken);

        await migrator.MigrateAsync(
            PostgresContainerFixture.CompleteSprint3Lote3GMigration,
            cancellationToken);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

        Assert.True(rollbackIsComplete);
        Assert.Equal(PostgresContainerFixture.CompleteSprint3Lote3GMigration, applied.Last());
    }
}
