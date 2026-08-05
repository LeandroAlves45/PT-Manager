using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Infrastructure.IntegrationTests.Migrations;

/// <summary>
/// Provar que a InitialCreate sobe em uma DB vazia. Após os testes desce até zero permitindo
/// reutilizar o mesmo container para outros testes.
/// </summary>
public sealed class MigrationLifecycleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ptmanager_migration_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public PtManagerDbContext CreateContext()
    {
        var tenantContext = Support.TestTenantContext.Administrator();
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new PtManagerDbContext(options, tenantContext);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync(cancellationToken);
    }

    public async Task<int> CountApplicationTablesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE'
                AND table_name <> '__EFMigrationsHistory';
        """;

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetApplicationTableNamesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE'
                AND table_name <> '__EFMigrationsHistory'
            ORDER BY table_name;
        """;

        var tableNames = new List<string>();
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            tableNames.Add(reader.GetString(0));

        return tableNames;
    }

    public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                    AND table_type = 'BASE TABLE'
                    AND table_name = @table_name
            );
        """;

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        // O parâmetro evita interpolar um identificador recebido pelo teste.
        command.Parameters.AddWithValue("table_name", tableName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Table existence query returned null."));
    }
}
