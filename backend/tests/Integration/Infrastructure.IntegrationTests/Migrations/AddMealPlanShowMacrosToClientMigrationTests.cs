using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.IntegrationTests.Migrations;

/// <summary>
/// Prova que a coluna <c>meal_plans.show_macros_to_client</c> entra NOT NULL com default true (os
/// planos que já existiam continuam a mostrar as macros, como antes) e que o Down a retira.
/// </summary>
[Collection(MigrationLifecycleCollection.Name)]
public sealed class AddMealPlanShowMacrosToClientMigrationTests : IAsyncLifetime
{
    private readonly MigrationLifecycleFixture _fixture;

    public AddMealPlanShowMacrosToClientMigrationTests(MigrationLifecycleFixture fixture) =>
        _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_AddsNotNullColumnDefaultTrue_AndRollbackDropsIt()
    {
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddTrainerAccessBlocksAndModerationNotesMigration, Token);
        Assert.False(await ColumnExistsAsync());

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddMealPlanShowMacrosToClientMigration, Token);
        Assert.True(await ColumnExistsAsync());
        Assert.Equal("NO", await ColumnAttributeAsync("is_nullable"));
        Assert.Equal("true", await ColumnAttributeAsync("column_default"));

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddTrainerAccessBlocksAndModerationNotesMigration, Token);
        Assert.False(await ColumnExistsAsync());
        Assert.True(await _fixture.TableExistsAsync("meal_plans", Token));

        await migrator.MigrateAsync(
            PostgresContainerFixture.AddMealPlanShowMacrosToClientMigration, Token);
        Assert.True(await ColumnExistsAsync());
    }

    private async Task<bool> ColumnExistsAsync() =>
        await _fixture.QueryScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'meal_plans' AND column_name = 'show_macros_to_client')
            """,
            Token);

    // Nome da coluna do information_schema, nunca vindo do utilizador.
    private async Task<string?> ColumnAttributeAsync(string attribute) =>
        await _fixture.QueryScalarAsync<string>(
            $"""
            SELECT {attribute}::text FROM information_schema.columns
            WHERE table_name = 'meal_plans' AND column_name = 'show_macros_to_client'
            """,
            Token);
}
