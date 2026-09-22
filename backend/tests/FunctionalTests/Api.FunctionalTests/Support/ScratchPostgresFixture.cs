using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Container PostgreSQL próprio de uma classe de testes, onde cada cenário cria a sua
/// base de dados vazia.
/// </summary>
/// <remarks>
/// Os testes de health e de seed precisam de estados que a coleção partilhada não pode
/// ter: schema ausente, migration pendente, ou catálogo global semeado que alteraria as
/// contagens dos restantes testes. Uma base por cenário mantém-nos independentes.
/// </remarks>
public sealed class ScratchPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("ptmanager_scratch")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Cria uma base de dados vazia, sem tabelas nem histórico de migrations.</summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = $"scratch_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = name
        }.ConnectionString;
    }

    /// <summary>
    /// Aplica migrations explicitamente, até <paramref name="targetMigration"/> ou até à última.
    /// </summary>
    /// <remarks>
    /// Usa um host <c>Testing</c>, onde o seed nem sequer é registado: a base fica migrada
    /// antes de qualquer host com seed ativo arrancar.
    /// </remarks>
    public static async Task MigrateAsync(
        string connectionString,
        CancellationToken cancellationToken,
        string? targetMigration = null)
    {
        using var factory = new ApiWebApplicationFactory(connectionString);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        if (targetMigration is null)
            await context.Database.MigrateAsync(cancellationToken);
        else
            await context.GetService<IMigrator>().MigrateAsync(targetMigration, cancellationToken);
    }

    /// <summary>Executa uma consulta escalar diretamente, sem o DbContext nem filtros de tenant.</summary>
    public static async Task<long> ScalarAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <summary>Executa um comando SQL direto, para simular estado parcial ou avariado.</summary>
    public static async Task ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
