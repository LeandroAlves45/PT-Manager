using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

[Collection(MigrationLifecycleCollection.Name)]
public sealed class AddQStashDispatchReceiptsMigrationTests : IAsyncLifetime
{
    private readonly MigrationLifecycleFixture _fixture;

    public AddQStashDispatchReceiptsMigrationTests(MigrationLifecycleFixture fixture) =>
        _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_WhenExternalIdentitiesExists_AppliesQStashReceipts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.AddExternalIdentitiesMigration,
            cancellationToken);

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddQStashDispatchReceiptsMigration,
            cancellationToken);

        Assert.True(await ReceiptsSchemaExistsAsync(cancellationToken));
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        Assert.Equal(
            PostgresContainerFixture.AddQStashDispatchReceiptsMigration,
            applied.Last());
    }

    [Fact]
    public async Task Migrate_WhenQStashReceiptsWasRolledBack_CanApplyAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.AddQStashDispatchReceiptsMigration,
            cancellationToken);

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddExternalIdentitiesMigration,
            cancellationToken);
        Assert.False(await _fixture.TableExistsAsync(
            "qstash_dispatch_receipts", cancellationToken));

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddQStashDispatchReceiptsMigration,
            cancellationToken);

        Assert.True(await ReceiptsSchemaExistsAsync(cancellationToken));
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        Assert.Equal(
            PostgresContainerFixture.AddQStashDispatchReceiptsMigration,
            applied.Last());
    }

    private async Task<bool> ReceiptsSchemaExistsAsync(CancellationToken cancellationToken) =>
        await _fixture.QueryScalarAsync<bool>(
            """
            SELECT
                to_regclass('public.qstash_dispatch_receipts') IS NOT NULL
                AND to_regclass('public.ix_qstash_dispatch_receipts_token_expires_at') IS NOT NULL
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                        AND table_name = 'qstash_dispatch_receipts'
                        AND column_name = 'jti_hash'
                        AND data_type = 'character'
                        AND character_maximum_length = 64);
            """,
            cancellationToken);
}
