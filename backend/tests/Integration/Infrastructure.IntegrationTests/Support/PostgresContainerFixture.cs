using Domain.Entities.Clients;
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
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext(trainerId: null);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public PtManagerDbContext CreateContext(Guid? trainerId)
    {
        var tenantContext = new TestTenantContext(trainerId);
        var interceptor = new TenantWriteValidationInterceptor(tenantContext);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptor)
            .EnableDetailedErrors()
            .Options;

        return new PtManagerDbContext(options, tenantContext);
    }

    public async Task<TestTenantSeed> SeedTenantWithClientAsync(
        string discriminator,
        CancellationToken cancellationToken = default)
    {
        var now = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var trainer = new User(
            new EmailAddress($"trainer-{discriminator}@example.test"),
            role: "trainer",
            fullName: "Trainer Test",
            now);
        var clientUser = new User(
            new EmailAddress($"client-{discriminator}@example.test"),
            role: "client",
            fullName: "Client Test",
            now);
        var client = new Client(
            trainer.Id,
            clientUser.Id,
            name: "Client Test",
            objective: null,
            now);

        await using var context = CreateContext(trainer.Id);
        context.Users.AddRange(trainer, clientUser);
        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        return new TestTenantSeed(trainer.Id, client.Id);
    }

    public async Task<T?> QueryScalarAsync<T>(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public async Task<int> ExecuteSqlAsync(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public sealed record TestTenantSeed(Guid TrainerId, Guid ClientId);
}
