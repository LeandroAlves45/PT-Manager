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

    /// <summary>
    /// O frontend lê <c>correlation_id</c> e <c>errors</c> dos tipos gerados. Sem este
    /// schema, ambos existiriam em runtime mas não no contrato.
    /// </summary>
    [Fact]
    public async Task OpenApiDocument_DeclaresApiProblemDetailsWithCorrelationIdAndErrors()
    {
        using var document = await GetOpenApiDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var problem = PropertyNames(schemas.GetProperty("ApiProblemDetails"));
        var validationError = PropertyNames(schemas.GetProperty("ApiValidationError"));

        Assert.Superset(
            new HashSet<string> { "type", "title", "status", "detail", "instance", "correlation_id", "errors" },
            problem);
        Assert.Equal(["code", "field", "message"], validationError.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Uma resposta 2xx sem <c>content</c> gera <c>content?: never</c> no cliente
    /// TypeScript e obriga a casts manuais. Só o 204 pode não ter corpo.
    /// </summary>
    [Fact]
    public async Task OpenApiDocument_EverySuccessResponseExceptNoContentHasBody()
    {
        using var document = await GetOpenApiDocumentAsync();
        var violations = new List<string>();
        var successResponses = 0;

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var operationId = $"{operation.Name.ToUpperInvariant()} {path.Name}";
                var success = operation.Value.TryGetProperty("responses", out var responses)
                    ? responses.EnumerateObject()
                        .Where(candidate => candidate.Name.StartsWith('2'))
                        .ToArray()
                    : [];

                // Sem atributo de sucesso, o gerador não inventa 200: a operação fica só com erros.
                if (success.Length == 0)
                    violations.Add($"{operationId} declares no 2xx response");

                foreach (var response in success)
                {
                    successResponses++;
                    var hasContent = response.Value.TryGetProperty("content", out _);
                    var id = $"{operationId} {response.Name}";

                    if (response.Name == "204" && hasContent)
                        violations.Add($"{id} declares a body");
                    else if (response.Name != "204" && !hasContent)
                        violations.Add($"{id} has no body");
                }
            }
        }

        Assert.True(successResponses >= 170, $"Only {successResponses} success responses found.");
        Assert.Empty(violations);
    }

    /// <summary>
    /// A leitura tolerante de números em string não descreve o que a API escreve.
    /// Deixar a união no schema tornava todos os números <c>number | string</c> no frontend.
    /// </summary>
    [Fact]
    public async Task OpenApiDocument_NumericSchemasAreNotUnionsWithString()
    {
        using var document = await GetOpenApiDocumentAsync();
        var numericTypes = 0;
        var unions = new List<string>();

        Walk(document.RootElement.GetProperty("components"), "components");

        Assert.True(numericTypes > 0, "No numeric schema found; the check would be vacuous.");
        Assert.Empty(unions);

        void Walk(JsonElement element, string location)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Walk(item, $"{location}[{index++}]");
                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
                return;

            if (element.TryGetProperty("type", out var type))
            {
                var types = type.ValueKind == JsonValueKind.Array
                    ? type.EnumerateArray().Select(value => value.GetString()).ToArray()
                    : [type.ValueKind == JsonValueKind.String ? type.GetString() : null];

                var numeric = types.Contains("integer") || types.Contains("number");
                if (numeric)
                    numericTypes++;
                if (numeric && types.Contains("string"))
                    unions.Add(location);
            }

            foreach (var property in element.EnumerateObject())
                Walk(property.Value, $"{location}.{property.Name}");
        }
    }

    public void Dispose() => _factory.Dispose();

    private static HashSet<string> PropertyNames(JsonElement schema) =>
        schema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

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
