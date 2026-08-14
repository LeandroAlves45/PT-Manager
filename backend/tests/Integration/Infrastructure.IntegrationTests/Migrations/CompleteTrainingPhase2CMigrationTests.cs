using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

[Collection(MigrationLifecycleCollection.Name)]
public sealed class CompleteTrainingPhase2CMigrationTests : IAsyncLifetime
{
    private const string CompleteTrainingPhase2CMigration =
        "20260814121132_CompleteTrainingPhase2C";
    private readonly MigrationLifecycleFixture _fixture;

    public CompleteTrainingPhase2CMigrationTests(MigrationLifecycleFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_WhenPhase2CWasRolledBack_CanApplyAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(CompleteTrainingPhase2CMigration, cancellationToken);
        await migrator.MigrateAsync(
            PostgresContainerFixture.InitialCreateMigration,
            cancellationToken);

        await migrator.MigrateAsync(CompleteTrainingPhase2CMigration, cancellationToken);
        var appliedMigrations = await context.Database
            .GetAppliedMigrationsAsync(cancellationToken);

        Assert.Equal(
            [
                PostgresContainerFixture.InitialCreateMigration,
                CompleteTrainingPhase2CMigration
            ],
            appliedMigrations);
    }
}
