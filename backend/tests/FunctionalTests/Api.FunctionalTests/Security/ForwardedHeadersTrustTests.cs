using System.Net;
using System.Net.Http.Json;
using Api.Configuration;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Security;

/// <summary>
/// PTM-SEC-03: sem proxies confiáveis, <c>X-Forwarded-For</c> não pode escolher a
/// partição de rate limit.
/// </summary>
/// <remarks>
/// Com <c>KnownProxies</c> e <c>KnownIPNetworks</c> vazios, o
/// <c>ForwardedHeadersMiddleware</c> aceita os headers de qualquer origem. Cada valor
/// novo de XFF criava então uma partição nova e anulava os limites por IP. O
/// <c>ApiRateLimitingTests</c> não apanhava o caso, porque o seu host não passa pelo
/// <c>UseForwardedHeaders</c>.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class ForwardedHeadersTrustTests : IAsyncLifetime
{
    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public ForwardedHeadersTrustTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_database.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Login_WithRotatingForwardedFor_IsStillRateLimitedWithoutTrustedProxies()
    {
        var client = _factory.CreateOriginClient();
        var statuses = new List<HttpStatusCode>();

        // O limite de login é 10 por minuto por IP.
        for (var attempt = 1; attempt <= 11; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    email = $"xff-{Guid.NewGuid():N}@example.test",
                    password = "Wrong-Password-9!"
                })
            };
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{attempt}");

            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public void AddApiForwardedHeaders_WithoutTrustedProxies_DisablesProcessing()
    {
        var options = ResolveOptions(Environments.Development, new Dictionary<string, string?>());

        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void AddApiForwardedHeaders_WithKnownNetwork_ProcessesOnlyThatNetwork()
    {
        var options = ResolveOptions(
            Environments.Production,
            new Dictionary<string, string?> { ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8" });

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal(1, options.ForwardLimit);
    }

    [Fact]
    public void AddApiForwardedHeaders_InProductionWithoutTrustedProxies_FailsAtStartup()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ResolveOptions(Environments.Production, new Dictionary<string, string?>()));
    }

    private static ForwardedHeadersOptions ResolveOptions(
        string environmentName,
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var environment = new HostingEnvironment { EnvironmentName = environmentName };

        var services = new ServiceCollection();
        services.AddApiForwardedHeaders(configuration, environment);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
