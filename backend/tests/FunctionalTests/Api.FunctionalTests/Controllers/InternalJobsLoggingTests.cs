using System.Net;
using System.Security.Cryptography;
using System.Text;
using Api.FunctionalTests.Support;
using Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Sentinela de logging do receptor interno.
/// Um log de diagnóstico é um sink persistente. Se a assinatura, o `jti` ou o
/// raw body chegassem lá, o material que autentica a activação passaria a estar
/// disponível a quem só tem acesso de leitura aos logs.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class InternalJobsLoggingTests : IDisposable
{
    private const string DispatchPath = "/api/internal/jobs/dispatch";
    private const string CanonicalUrl = "https://localhost/api/internal/jobs/dispatch";

    private static readonly string CurrentKey =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly CapturingLoggerProvider _logs = new();
    private readonly ApiWebApplicationFactory _factory;

    public InternalJobsLoggingTests(PostgresApiFixture fixture)
    {
        _factory = new ApiWebApplicationFactory(fixture.ConnectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["QStash:Enabled"] = "true",
                ["QStash:CurrentSigningKey"] = CurrentKey,
                ["QStash:NextSigningKey"] = CurrentKey + "aa",
                ["QStash:DestinationUrl"] = CanonicalUrl,
                ["QStash:MaximumBodySize"] = "4096"
            },
            ConfigureServices = services =>
                services.AddSingleton<ILoggerProvider>(_logs)
        };
    }

    public void Dispose()
    {
        _factory.Dispose();
        _logs.Dispose();
    }

    [Fact]
    public async Task Dispatch_WithInvalidSignature_NeverLogsTheSignatureMaterial()
    {
        var sentinel = "sentinel-signature-value-must-not-be-logged";
        using var client = CreateClient();

        var response = await SendAsync(client, "{}", sentinel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNotLogged(sentinel);
        AssertNotLogged(CurrentKey);
    }

    [Fact]
    public async Task Dispatch_WithValidSignature_NeverLogsTokenJtiOrBody()
    {
        var jti = "sentinel-jti-value-must-not-be-logged";
        var body = "{\"sentinel\":\"sentinel-body-value-must-not-be-logged\"}";
        var token = CreateToken(body, jti);

        using var client = CreateClient();
        var response = await SendAsync(client, body, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertNotLogged(jti);
        AssertNotLogged("sentinel-body-value-must-not-be-logged");
        AssertNotLogged(token);
        AssertNotLogged(CurrentKey);
    }

    [Fact]
    public async Task Dispatch_WithInvalidSignature_ProblemDetailsRevealsNoInternals()
    {
        using var client = CreateClient();

        var response = await SendAsync(client, "{}", "sentinel-bad-signature");
        var payload = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        // A resposta é fechada: nem eco da assinatura, nem detalhe do motivo.
        Assert.DoesNotContain("sentinel-bad-signature", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(CurrentKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("jti", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_RejectionIsLoggedWithAStableEventId()
    {
        using var client = CreateClient();

        await SendAsync(client, "{}", "not-a-jwt");

        // A rejeição tem de ser observável para alerta, mesmo sem detalhe sensível.
        Assert.Contains(
            _logs.Snapshot(),
            entry => entry.LogLevel >= LogLevel.Warning && entry.EventId.Id != 0);
    }

    [Fact]
    public async Task Dispatch_ReplayOfTheSameToken_DoesNotLogASecondActivation()
    {
        var body = "{}";
        var token = CreateToken(body, Guid.NewGuid().ToString("N"));
        using var client = CreateClient();

        var first = await SendAsync(client, body, token);
        var replay = await SendAsync(client, body, token);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal(
            1,
            _logs.Snapshot().Count(entry =>
                entry.EventId == JobDispatchLogEvents.ActivationStarted));
    }

    private void AssertNotLogged(string sentinel)
    {
        foreach (var entry in _logs.Snapshot())
        {
            Assert.DoesNotContain(sentinel, entry.FormattedMessage, StringComparison.Ordinal);

            foreach (var value in entry.State.Values)
                Assert.DoesNotContain(
                    sentinel, value?.ToString() ?? string.Empty, StringComparison.Ordinal);

            Assert.DoesNotContain(
                sentinel, entry.Exception?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
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

    private static string CreateToken(string body, string jti)
    {
        var now = DateTime.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CurrentKey));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Upstash",
            Subject = new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", CanonicalUrl),
                new System.Security.Claims.Claim("jti", jti),
                new System.Security.Claims.Claim(
                    "body",
                    Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(body))))
            ]),
            IssuedAt = now,
            NotBefore = now.AddSeconds(-5),
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
    }
}
