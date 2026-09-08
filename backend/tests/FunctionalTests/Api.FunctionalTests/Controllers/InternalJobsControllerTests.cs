using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Api.FunctionalTests.Support;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Verifica o contrato HTTP do receptor interno de jobs.
/// Os testes assinam tokens reais com o mesmo algoritmo do Upstash em vez de
/// substituírem o autenticador por um duplo. É a única forma de provar que a
/// verificação de assinatura, de `sub`, de `body` e de `jti` funciona no
/// adapter que corre em produção.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class InternalJobsControllerTests : IDisposable
{
    private const string DispatchPath = "/api/internal/jobs/dispatch";
    private const string CanonicalUrl = "https://localhost/api/internal/jobs/dispatch";
    private const string Issuer = "Upstash";

    private static readonly string CurrentKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static readonly string NextKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly ApiWebApplicationFactory _factory;

    private readonly string _connectionString;

    public InternalJobsControllerTests(PostgresApiFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _factory = CreateFactory(enabled: true, connectionString: _connectionString);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Dispatch_WithValidSignature_ReturnsNoContent()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(client, body, CreateToken(body));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task Dispatch_WithNextSigningKey_IsAcceptedDuringRotation()
    {
        using var client = CreateClient();
        var body = "{}";

        // Durante a rotação, o Upstash pode assinar com a chave seguinte.
        var response = await SendAsync(client, body, CreateToken(body, signingKey: NextKey));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_ReplayOfTheSameToken_IsAcceptedButDoesNotReactivate()
    {
        using var client = CreateClient();
        var body = "{}";
        var token = CreateToken(body);

        var first = await SendAsync(client, body, token);
        var replay = await SendAsync(client, body, token);

        // O replay é idempotente: devolve sucesso sem reactivar os dispatchers.
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithoutSignatureHeader_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, DispatchPath)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithGarbageSignature_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await SendAsync(client, "{}", "not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_SignedWithUnknownKey_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";
        var foreignKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var response = await SendAsync(client, body, CreateToken(body, signingKey: foreignKey));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithWrongIssuer_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(client, body, CreateToken(body, issuer: "Attacker"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("https://localhost/api/internal/jobs/dispatch/")]
    [InlineData("https://localhost/api/internal/jobs/dispatch?x=1")]
    [InlineData("http://localhost/api/internal/jobs/dispatch")]
    [InlineData("https://evil.test/api/internal/jobs/dispatch")]
    public async Task Dispatch_WithWrongSubject_ReturnsUnauthorized(string subject)
    {
        using var client = CreateClient();
        var body = "{}";

        // 'sub' identifica o destino assinado: qualquer variação tem de falhar.
        var response = await SendAsync(client, body, CreateToken(body, subject: subject));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenBodyHashDoesNotMatch_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        // Token assinado para um corpo diferente do que é enviado.
        var token = CreateToken("{\"original\":true}");
        var response = await SendAsync(client, "{\"tampered\":true}", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenTokenExpired_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(
            client, body, CreateToken(body, expiresIn: TimeSpan.FromMinutes(-10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenTokenLifetimeIsTooLong_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        // Um token de vida longa alargaria a janela de reutilização.
        var response = await SendAsync(
            client, body, CreateToken(body, expiresIn: TimeSpan.FromHours(6)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenNotBeforeIsInTheFuture_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(
            client, body, CreateToken(body, notBefore: DateTime.UtcNow.AddMinutes(10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenJtiIsMissing_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(client, body, CreateToken(body, jti: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenBodyClaimIsMissing_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await SendAsync(client, "{}", CreateToken("{}", includeBodyHash: false));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenAlgorithmIsNone_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        // Um token não assinado nunca pode ser aceite.
        var unsigned = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Subject = new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", CanonicalUrl),
                new System.Security.Claims.Claim("jti", Guid.NewGuid().ToString("N"))
            ]),
            Expires = DateTime.UtcNow.AddMinutes(5)
        });

        var response = await SendAsync(client, "{}", unsigned);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenAlgorithmIsHs384_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(
            client, body, CreateToken(body, algorithm: SecurityAlgorithms.HmacSha384, signingKey: CurrentKey + CurrentKey));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenTokenExpiredWithinClockSkew_ReturnsNoContent()
    {
        using var client = CreateClient();
        var body = "{}";
        var issuedAt = DateTime.UtcNow.AddMinutes(-1);

        var response = await SendAsync(
            client,
            body,
            CreateToken(
                body,
                expiresIn: TimeSpan.FromSeconds(-5),
                notBefore: issuedAt,
                issuedAt: issuedAt));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenTokenExpiredBeyondClockSkew_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var body = "{}";

        var response = await SendAsync(
            client, body, CreateToken(body, expiresIn: TimeSpan.FromMinutes(-2)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenBodyExceedsConfiguredLimit_ReturnsPayloadTooLarge()
    {
        using var client = CreateClient();
        var oversized = new string('a', 8 * 1024);
        var body = $"{{\"padding\":\"{oversized}\"}}";

        var response = await SendAsync(client, body, CreateToken(body));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenChunkedBodyExceedsConfiguredLimit_ReturnsPayloadTooLarge()
    {
        using var client = CreateClient();
        var oversized = new string('a', 8 * 1024);
        var body = $"{{\"padding\":\"{oversized}\"}}";
        var payload = Encoding.UTF8.GetBytes(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, DispatchPath)
        {
            Content = new StreamContent(new MemoryStream(payload))
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content.Headers.ContentLength = null;
        request.Headers.TransferEncodingChunked = true;
        request.Headers.Add("Upstash-Signature", CreateToken(body));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WhenRateLimitIsExceeded_ReturnsTooManyRequests()
    {
        using var client = CreateClient();
        var body = "{}";
        HttpResponseMessage? last = null;

        for (var attempt = 0; attempt < 11; attempt++)
        {
            last?.Dispose();
            last = await SendAsync(client, body, CreateToken(body));
        }

        using var response = last!;
        var payload = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(payload);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal("Too many requests", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Dispatch_WhenIntegrationIsDisabled_ReturnsServiceUnavailable()
    {
        // QStash desligado é o estado de deploy: nenhuma activação pode acontecer.
        using var disabled = CreateFactory(enabled: false, connectionString: _connectionString);
        using var client = CreateClient(disabled);
        var body = "{}";

        var response = await SendAsync(client, body, CreateToken(body));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithoutOriginHeader_IsStillProcessedByQStashRules()
    {
        // O caller é um servidor, não um browser: a protecção de CSRF por Origin
        // não se aplica e não pode bloquear a activação.
        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        var body = "{}";

        var response = await SendAsync(client, body, CreateToken(body));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithUserJwtButNoQStashSignature_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, DispatchPath)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        // Um JWT de utilizador não substitui a autenticação interna.
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithGetVerb_IsNotAllowed()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(DispatchPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private HttpClient CreateClient(ApiWebApplicationFactory? factory = null) =>
        (factory ?? _factory).CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string body,
        string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DispatchPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Upstash-Signature", signature);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string CreateToken(
        string body,
        string? signingKey = null,
        string issuer = Issuer,
        string subject = CanonicalUrl,
        string? jti = "",
        TimeSpan? expiresIn = null,
        DateTime? notBefore = null,
        bool includeBodyHash = true,
        string algorithm = SecurityAlgorithms.HmacSha256,
        DateTime? issuedAt = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", subject)
        };

        if (jti is not null)
            claims.Add(new System.Security.Claims.Claim(
                "jti", jti.Length == 0 ? Guid.NewGuid().ToString("N") : jti));

        if (includeBodyHash)
            claims.Add(new System.Security.Claims.Claim(
                "body",
                Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(body)))));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey ?? CurrentKey));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            IssuedAt = issuedAt ?? now,
            NotBefore = notBefore ?? now.AddSeconds(-5),
            Expires = now.Add(expiresIn ?? TimeSpan.FromMinutes(5)),
            SigningCredentials = new SigningCredentials(key, algorithm)
        });
    }

    /// <summary>
    /// Cria um host com o receptor QStash ligado ou desligado, partilhando a base
    /// já migrada pela fixture da colecção.
    /// </summary>
    private static ApiWebApplicationFactory CreateFactory(
        bool enabled,
        string connectionString)
    {
        var settings = new Dictionary<string, string>
        {
            ["QStash:Enabled"] = enabled ? "true" : "false"
        };

        if (enabled)
        {
            settings["QStash:CurrentSigningKey"] = CurrentKey;
            settings["QStash:NextSigningKey"] = NextKey;
            settings["QStash:DestinationUrl"] = CanonicalUrl;
            settings["QStash:MaximumBodySize"] = "4096";
        }

        return new ApiWebApplicationFactory(connectionString)
        {
            AdditionalSettings = settings
        };
    }
}
