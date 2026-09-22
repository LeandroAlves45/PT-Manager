using System.Net;
using Api.FunctionalTests.Support;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Health;

/// <summary>
/// Prova as sondas de vivacidade e readiness contra PostgreSQL real (QG6C-HEALTH-001).
/// </summary>
/// <remarks>
/// <para>
/// <c>live</c> diz se o processo responde e nunca consulta dependências: um orquestrador
/// que reinicia a API porque a base caiu só agrava a indisponibilidade.
/// </para>
/// <para>
/// <c>ready</c> diz se a instância pode receber tráfego: exige ligação e zero migrations
/// pendentes, e nunca aplica migrations para "se reparar".
/// </para>
/// </remarks>
public sealed class HealthChecksTests : IClassFixture<ScratchPostgresFixture>
{
    private const string LiveRoute = "/health/live";
    private const string ReadyRoute = "/health/ready";

    /// <summary>Porta 1 recusa a ligação de imediato; Timeout curto limita os retries.</summary>
    private const string UnavailableConnectionString =
        "Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Password=unused;Timeout=2";

    /// <summary>Acima do orçamento anónimo global de 60 pedidos por minuto e por IP.</summary>
    private const int RequestsAboveGlobalLimit = 70;

    private readonly ScratchPostgresFixture _postgres;

    public HealthChecksTests(ScratchPostgresFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Live_WithUnavailableDatabase_ReturnsOk()
    {
        using var factory = new ApiWebApplicationFactory(UnavailableConnectionString);

        using var response = await CreateClient(factory).GetAsync(LiveRoute, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task Ready_WithCurrentSchema_ReturnsOk()
    {
        var connectionString = await _postgres.CreateDatabaseAsync(Cancellation);
        await ScratchPostgresFixture.MigrateAsync(connectionString, Cancellation);
        using var factory = new ApiWebApplicationFactory(connectionString);

        using var response = await CreateClient(factory).GetAsync(ReadyRoute, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task Ready_WithoutSchema_ReturnsServiceUnavailableAndDoesNotMigrate()
    {
        var connectionString = await _postgres.CreateDatabaseAsync(Cancellation);
        using var factory = new ApiWebApplicationFactory(connectionString);

        using var response = await CreateClient(factory).GetAsync(ReadyRoute, Cancellation);

        await AssertUnavailableWithoutDetailsAsync(response);

        // A sonda nunca repara o schema: a base continua sem nenhuma tabela.
        Assert.Equal(0, await ScratchPostgresFixture.ScalarAsync(
            connectionString,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'",
            Cancellation));
    }

    [Fact]
    public async Task Ready_WithPendingMigration_ReturnsServiceUnavailable()
    {
        var connectionString = await _postgres.CreateDatabaseAsync(Cancellation);
        using var factory = new ApiWebApplicationFactory(connectionString);
        var migrations = ReadMigrations(factory);
        await ScratchPostgresFixture.MigrateAsync(
            connectionString, Cancellation, targetMigration: migrations[^2]);

        using var response = await CreateClient(factory).GetAsync(ReadyRoute, Cancellation);

        await AssertUnavailableWithoutDetailsAsync(response);
        Assert.Equal(1, await CountPendingMigrationsAsync(factory));
    }

    [Fact]
    public async Task Ready_WithUnavailableDatabase_ReturnsServiceUnavailable()
    {
        using var factory = new ApiWebApplicationFactory(UnavailableConnectionString);

        using var response = await CreateClient(factory).GetAsync(ReadyRoute, Cancellation);

        await AssertUnavailableWithoutDetailsAsync(response);
    }

    [Theory]
    [InlineData(LiveRoute)]
    [InlineData(ReadyRoute)]
    public async Task HealthEndpoint_AboveGlobalAnonymousLimit_IsNeverRateLimited(string route)
    {
        var connectionString = await _postgres.CreateDatabaseAsync(Cancellation);
        await ScratchPostgresFixture.MigrateAsync(connectionString, Cancellation);
        using var factory = new ApiWebApplicationFactory(connectionString);

        // Um único cliente é uma única partição de IP, a mesma que uma sonda real usaria.
        var client = CreateClient(factory);
        var statuses = new List<HttpStatusCode>();
        for (var request = 0; request < RequestsAboveGlobalLimit; request++)
        {
            using var response = await client.GetAsync(route, Cancellation);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    private static HttpClient CreateClient(ApiWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    /// <summary>
    /// O corpo da sonda é público e anónimo: não pode expor nomes de migrations,
    /// mensagens de exceção nem a connection string.
    /// </summary>
    private static async Task AssertUnavailableWithoutDetailsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static string[] ReadMigrations(ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PtManagerDbContext>()
            .Database.GetMigrations()
            .ToArray();
    }

    private static async Task<int> CountPendingMigrationsAsync(ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>()
            .Database.GetPendingMigrationsAsync(Cancellation))
            .Count();
    }
}
