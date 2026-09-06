using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Api.FunctionalTests.Support;

/// <summary>Gere o container e aplica migrations de forma explícita para a suite HTTP.</summary>
public sealed class PostgresApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("ptmanager_api_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public ApiWebApplicationFactory Factory { get; private set; } = null!;

    /// <summary>
    /// Connection string do container, para uma classe que precise de uma factory
    /// própria — por exemplo para substituir uma porta via ConfigureServices —
    /// continuando a partilhar a mesma base já migrada.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new ApiWebApplicationFactory(_container.GetConnectionString());

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        // Os testes controlam migrations; a API nunca as executa no arranque.
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Factory?.Dispose();
        await _container.DisposeAsync();
    }
}
