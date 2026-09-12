using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Infrastructure.IntegrationTests.Migrations;

/// <summary>
/// Prova o ponto 8 do Gate 5C: a migration aplica, reverte e reaplica em
/// PostgreSQL 17, e os dois preflights que o EF não gera bloqueiam exatamente os
/// estados que perderiam a capacidade de eliminar assets.
/// </summary>
[Collection(MigrationLifecycleCollection.Name)]
public sealed class AddManagedImageAssetsMigrationTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly MigrationLifecycleFixture _fixture;

    public AddManagedImageAssetsMigrationTests(MigrationLifecycleFixture fixture) =>
        _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ResetAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migrate_FromStripeBilling_AddsColumnAndBothPairConstraints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddStripeBillingOperationsMigration, cancellationToken);

        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken);

        Assert.True(await SchemaExistsAsync(cancellationToken));
    }

    [Fact]
    public async Task Rollback_WithoutManagedAvatars_RemovesSchemaAndCanApplyAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken);

        await migrator.MigrateAsync(PostgresContainerFixture.AddStripeBillingOperationsMigration, cancellationToken);
        Assert.False(await SchemaExistsAsync(cancellationToken));

        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken);
        Assert.True(await SchemaExistsAsync(cancellationToken));
        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        Assert.Equal(PostgresContainerFixture.AddManagedImageAssetsMigration, applied.Last());
    }

    /// <summary>
    /// Um avatar_url pré-existente não tem identificador e nunca poderia ser
    /// eliminado. O preflight recusa aplicar em vez de deixar o ADD CONSTRAINT
    /// falhar com uma violação genérica a meio do deploy.
    /// </summary>
    [Fact]
    public async Task Migrate_WhenAnUnmanagedAvatarExists_IsBlockedByPreflight()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken);
        var clientId = await SeedClientAsync(avatar: false, cancellationToken);
        await migrator.MigrateAsync(PostgresContainerFixture.AddStripeBillingOperationsMigration, cancellationToken);
        await _fixture.ExecuteSqlAsync(
            "UPDATE clients SET avatar_url = 'https://legacy.example.com/a.png' WHERE id = @id",
            cancellationToken,
            new NpgsqlParameter("id", clientId));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken));

        Assert.Contains("Migration blocked", exception.MessageText, StringComparison.Ordinal);
        Assert.False(await SchemaExistsAsync(cancellationToken));
    }

    /// <summary>
    /// Reverter com avatares geridos apagaria avatar_public_id e deixaria
    /// assets no storage sem referência que permitisse eliminá-los.
    /// </summary>
    [Fact]
    public async Task Rollback_WithManagedAvatars_IsBlockedByPreflight()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken);
        await SeedClientAsync(avatar: true, cancellationToken);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(PostgresContainerFixture.AddStripeBillingOperationsMigration, cancellationToken));

        Assert.Contains("Rollback blocked", exception.MessageText, StringComparison.Ordinal);
        Assert.True(await SchemaExistsAsync(cancellationToken));
    }

    [Fact]
    public async Task Migrate_WhenLogoPairIsIncomplete_IsBlockedBeforeSchemaChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken);
        await SeedClientAsync(avatar: false, cancellationToken);
        await migrator.MigrateAsync(PostgresContainerFixture.AddStripeBillingOperationsMigration, cancellationToken);
        await _fixture.ExecuteSqlAsync(
            "UPDATE trainer_settings SET logo_url = 'https://legacy.example.com/logo.png'",
            cancellationToken);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            migrator.MigrateAsync(PostgresContainerFixture.AddManagedImageAssetsMigration, cancellationToken));

        Assert.Contains("trainer_settings", exception.MessageText, StringComparison.Ordinal);
        Assert.False(await SchemaExistsAsync(cancellationToken));
        Assert.DoesNotContain(PostgresContainerFixture.AddManagedImageAssetsMigration,
            await context.Database.GetAppliedMigrationsAsync(cancellationToken));
    }

    private async Task<Guid> SeedClientAsync(bool avatar, CancellationToken cancellationToken)
    {
        var trainer = CreateUser("trainer", Guid.NewGuid());
        var clientUser = CreateUser("client", Guid.NewGuid());
        var client = new Client(
            trainer.Id, "Migration Client", clientUser.Email, "+351911111111",
            BirthDate.Create(new DateOnly(1990, 1, 1), DateOnly.FromDateTime(Now)),
            BiologicalSex.Female, null, null, null, null, Now);
        client.AttachUser(clientUser.Id, Now);
        if (avatar)
            client.ReplaceAvatar("https://res.cloudinary.com/demo/a.webp", "pt-manager/trainers/t/avatars/a", Now);

        await using var context = _fixture.CreateContext();
        context.Users.AddRange(trainer, clientUser);
        context.Clients.Add(client);
        context.TrainerSettings.Add(new TrainerSettingsEntity(trainer.Id, Now));
        await context.SaveChangesAsync(cancellationToken);
        return client.Id;
    }

    private static User CreateUser(string role, Guid discriminator)
    {
        var user = new User(new EmailAddress($"{role}-{discriminator:N}@example.test"), role, "Migration Test", Now);
        user.SetPasswordHash("migration-test-password-hash", Now);
        return user;
    }

    private Task<bool> SchemaExistsAsync(CancellationToken cancellationToken) =>
        _fixture.QueryScalarAsync<bool>(
            """
            SELECT
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                        AND table_name = 'clients'
                        AND column_name = 'avatar_public_id'
                        AND character_maximum_length = 500)
                AND EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_clients_avatar_pair')
                AND EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_trainer_settings_logo_pair');
            """,
            cancellationToken);
}
