using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

[Collection(MigrationLifecycleCollection.Name)]
public sealed class InitialCreateMigrationTests : IAsyncLifetime
{
    private readonly MigrationLifecycleFixture _fixture;

    public InitialCreateMigrationTests(MigrationLifecycleFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_WhenDatabaseIsEmpty_CreatesTwentyEightApplicationTables()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();

        // Act
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.InitialCreateMigration,
            cancellationToken);
        var tableCount = await _fixture.CountApplicationTablesAsync(cancellationToken);

        // Assert
        Assert.Equal(28, tableCount);
    }

    [Fact]
    public async Task MigrateZero_WhenInitialCreateWasApplied_RemovesApplicationTables()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.InitialCreateMigration,
            cancellationToken);

        // Act
        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken);
        var tableCount = await _fixture.CountApplicationTablesAsync(cancellationToken);

        // Assert
        Assert.Equal(0, tableCount);
    }

    [Fact]
    public async Task Migrate_WhenInitialCreateWasRolledBack_CanApplyAgain()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.InitialCreateMigration,
            cancellationToken);
        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken);

        // Act
        await migrator.MigrateAsync(
            PostgresContainerFixture.InitialCreateMigration,
            cancellationToken);
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

        // Assert
        Assert.Contains(PostgresContainerFixture.InitialCreateMigration, applied);
    }

    [Theory]
    [InlineData("clients")]
    [InlineData("initial_assessments")]
    [InlineData("client_supplement_assignments")]
    [InlineData("durable_jobs")]
    [InlineData("outbox_messages")]
    public async Task Migrate_WhenDatabaseIsEmpty_CreatesCriticalTables(string tableName)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            PostgresContainerFixture.InitialCreateMigration,
            cancellationToken);

        // Act
        var exists = await _fixture.TableExistsAsync(tableName, cancellationToken);

        // Assert
        Assert.True(exists);
    }
}
