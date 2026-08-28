using System.Net;
using Api.FunctionalTests.Support;
using Api.Middlewares;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.FunctionalTests.Http;

/// <summary>
/// Prova o pipeline HTTP composto contra a aplicação real e PostgreSQL real.
/// Requer Docker; a coleção partilha um único container.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ApiPipelineTests
{
    private readonly PostgresApiFixture _fixture;

    public ApiPipelineTests(PostgresApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundWithDefensiveHeaders()
    {
        var response = await CreateClient()
            .GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
    }

    [Fact]
    public async Task PlainHttpRequest_IsRedirectedToHttps()
    {
        var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        var response = await client.GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location!.Scheme);
    }

    [Fact]
    public async Task Preflight_AllowsTheConfiguredOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/does-not-exist");
        request.Headers.Add("Origin", "https://frontend.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://frontend.test",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Preflight_RejectsAnUnknownOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/does-not-exist");
        request.Headers.Add("Origin", "https://attacker.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OpenApiDocument_IsNotExposedOutsideDevelopment()
    {
        var response = await CreateClient()
            .GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Kestrel_DoesNotAdvertiseTheServerHeader()
    {
        var response = await CreateClient()
            .GetAsync("/does-not-exist", TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Server"));
    }

    private HttpClient CreateClient() =>
        _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
}
