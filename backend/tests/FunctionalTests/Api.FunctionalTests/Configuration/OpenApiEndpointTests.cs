using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.FunctionalTests.Configuration;

public sealed partial class OpenApiEndpointTests : IDisposable
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    private readonly ApiWebApplicationFactory _factory =
        new(UnusedConnectionString, "Development");

    [Fact]
    public async Task OpenApiDocument_InDevelopment_DeclaresHttpBearerJwtScheme()
    {
        using var response = await CreateClient()
            .GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(
            TestContext.Current.CancellationToken));

        var bearer = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task OpenApiDocument_ProtectedOperation_RequiresBearer()
    {
        using var document = await GetOpenApiDocumentAsync();

        var security = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/change-password")
            .GetProperty("post")
            .GetProperty("security");

        Assert.True(security[0].TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task OpenApiDocument_PublicOperation_DoesNotRequireBearer()
    {
        using var document = await GetOpenApiDocumentAsync();

        var login = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/login")
            .GetProperty("post");

        Assert.False(login.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task ScalarUi_InDevelopment_UsesNonceAllowedByContentSecurityPolicy()
    {
        using var response = await CreateClient()
            .GetAsync("/scalar/v1", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var decodedHtml = WebUtility.HtmlDecode(html);
        var nonce = ScalarNonceRegex().Match(html).Groups[1].Value;
        var contentSecurityPolicy = response.Headers
            .GetValues("Content-Security-Policy")
            .Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(nonce));
        Assert.Contains($"'nonce-{nonce}'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains(
            "\"preferredSecurityScheme\":[\"Bearer\"]",
            decodedHtml,
            StringComparison.Ordinal);
        Assert.Contains("\"agent\":{\"disabled\":true}", decodedHtml, StringComparison.Ordinal);
    }

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var response = await CreateClient()
            .GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("nonce=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ScalarNonceRegex();
}
