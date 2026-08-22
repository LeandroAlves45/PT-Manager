using Domain.Entities.Clients;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>
/// Gere um PostgreSQL descartável por classe de testes. Os testes usam GUIDs
/// próprios e consultam sempre pelos IDs criados, pelo que não dependem da
/// ordem nem de limpeza global entre métodos.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public const string InitialCreateMigration = "20260804163659_InitialCreate";
    public const string CompleteTrainingPhase2CMigration =
        "20260814121132_CompleteTrainingPhase2C";
    public const string CompleteSprint3Phase3Migration =
        "20260822155532_CompleteSprint3Phase3";
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ptmanager_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext(TestTenantContext.Administrator());
        // MigrateAsync valida o mesmo caminho de evolução de schema usado fora dos testes.
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public PtManagerDbContext CreateContext(Guid? trainerId) =>
        CreateContext(trainerId.HasValue
            ? TestTenantContext.ForTrainer(trainerId.Value)
            : TestTenantContext.WithoutTenant());

    public PtManagerDbContext CreateAdministrativeContext() =>
        CreateContext(TestTenantContext.Administrator());

    public PtManagerDbContext CreateAdministrativeContext(Guid actorUserId) =>
        CreateContext(TestTenantContext.Administrator(actorUserId));

    public async Task<TestTenantSeed> SeedTenantWithClientAsync(
        string discriminator,
        CancellationToken cancellationToken = default)
    {
        var now = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var trainer = CreateUser($"trainer-{discriminator}@example.test", "trainer", now);
        var clientUser = CreateUser($"client-{discriminator}@example.test", "client", now);
        var client = new Client(
            trainer.Id,
            "Client Test",
            clientUser.Email,
            "+351900000000",
            BirthDate.Create(new DateOnly(1995, 1, 1), DateOnly.FromDateTime(now)),
            BiologicalSex.Male,
            null,
            null,
            null,
            null,
            now);
        client.AttachUser(clientUser.Id, now);

        await using var context = CreateContext(trainer.Id);
        context.Users.AddRange(trainer, clientUser);
        context.Clients.Add(client);
        context.TrainerSettings.Add(new TrainerSettingsEntity(trainer.Id, now));
        await context.SaveChangesAsync(cancellationToken);

        return new TestTenantSeed(trainer.Id, client.Id, clientUser.Id);
    }

    public async Task<T?> QueryScalarAsync<T>(
        string sql,
        CancellationToken cancellationToken = default,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    public async Task<int> ExecuteSqlAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private PtManagerDbContext CreateContext(TestTenantContext tenantContext)
    {
        var interceptors = new TenantWriteValidationInterceptor(tenantContext);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptors)
            .EnableDetailedErrors()
            .Options;

        return new PtManagerDbContext(options, tenantContext);
    }

    private static User CreateUser(string email, string role, DateTime now)
    {
        var user = new User(new EmailAddress(email), role, "Integration Test", now);

        // O hash é opaco porque estes testes não exercitam autenticação.
        user.SetPasswordHash("integration-test-password-hash", now);
        return user;
    }

    public sealed record TestTenantSeed(Guid TrainerId, Guid ClientId, Guid ClientUserId);
}
