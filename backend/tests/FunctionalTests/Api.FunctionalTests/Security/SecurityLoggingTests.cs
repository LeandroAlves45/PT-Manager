using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Api.Authorization;
using Api.FunctionalTests.Support;
using Api.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Security;

/// <summary>Prova o contrato estruturado dos logs sem depender de um sink concreto.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class SecurityLoggingTests : IAsyncLifetime
{
    private const string EmailSentinel = "security-log-email-sentinel@example.test";
    private const string PasswordSentinel = "SECURITY-LOG-PASSWORD-SENTINEL";

    private readonly PostgresApiFixture _database;
    private readonly CapturingLoggerProvider _logs = new();
    private ApiWebApplicationFactory _factory = null!;

    public SecurityLoggingTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            ConfigureServices = services => services.AddSingleton<ILoggerProvider>(_logs)
        };
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task LoginRejection_UsesStableStructuredEventWithoutCredentials()
    {
        var client = _factory.CreateOriginClient();
        _logs.Clear();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = EmailSentinel, password = PasswordSentinel },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var entry = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.Login &&
            log.CategoryName.EndsWith("AuthController", StringComparison.Ordinal));
        Assert.Equal("login", entry.State["SecurityOperation"]);
        Assert.Equal("rejected", entry.State["SecurityOutcome"]);
        Assert.Null(entry.Exception);
        AssertLogsDoNotContainSentinels(_logs.Snapshot());
    }

    [Fact]
    public async Task InvalidBearer_LogsOnlyTheJwtRejectionCategory()
    {
        var userId = Guid.NewGuid();
        var bearerToken = TestJwtFactory.IssueWithSigningKey(
            userId,
            ApiRoleNames.Superuser,
            trainerId: null,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var client = _factory.CreateOriginClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        _logs.Clear();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/content-moderation/foods/{Guid.NewGuid()}/block",
            new { reason_code = "policy_violation" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var entry = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.JwtRejection);
        Assert.Contains(
            Assert.IsType<string>(entry.State["JwtRejectionCategory"]),
            new[] { "invalid_signature", "invalid_token" });
        Assert.Null(entry.Exception);
        AssertLogsDoNotContainSentinels(_logs.Snapshot(), bearerToken);
    }

    [Fact]
    public async Task MissingOrigin_LogsTheSafeRejectionCategory()
    {
        var client = _factory.CreateClient();
        _logs.Clear();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = EmailSentinel, password = PasswordSentinel },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var entry = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.OriginRejection);
        Assert.Equal("missing", entry.State["RejectionCategory"]);
        Assert.Null(entry.Exception);
        AssertLogsDoNotContainSentinels(_logs.Snapshot());
    }

    [Fact]
    public async Task RateLimitRejection_LogsPolicyWithoutRequestCredentials()
    {
        var client = _factory.CreateOriginClient();
        _logs.Clear();

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = EmailSentinel, password = PasswordSentinel },
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        var entry = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.RateLimitRejection);
        Assert.Equal("auth_login", entry.State["RateLimitPolicy"]);
        Assert.Null(entry.Exception);
        AssertLogsDoNotContainSentinels(_logs.Snapshot());
    }

    [Fact]
    public async Task RoleRejection_LogsCategoryWithoutBearerToken()
    {
        var bearerToken = TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid());
        var client = _factory.CreateOriginClient().WithBearer(bearerToken);
        _logs.Clear();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/invite-client",
            new { client_id = Guid.NewGuid(), email = EmailSentinel },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var entry = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.AuthorizationRejection);
        Assert.Equal("role", entry.State["AuthorizationRejectionCategory"]);
        AssertLogsDoNotContainSentinels(_logs.Snapshot(), bearerToken);
    }

    [Fact]
    public async Task InvalidTenantClaims_LogTenantRejectionWithoutBearerToken()
    {
        var userId = Guid.NewGuid();
        var bearerToken = TestJwtFactory.Issue(userId, ApiRoleNames.Trainer, Guid.NewGuid());
        var client = _factory.CreateOriginClient().WithBearer(bearerToken);
        _logs.Clear();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/invite-client",
            new { client_id = Guid.NewGuid(), email = EmailSentinel },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var entry = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.TenantRejection);
        Assert.Equal("invalid_identity_claims", entry.State["TenantRejectionCategory"]);
        AssertLogsDoNotContainSentinels(_logs.Snapshot(), bearerToken);
    }

    [Fact]
    public async Task AdministrativeModeration_LogsContextAndRejectedOutcome()
    {
        var bearerToken = TestJwtFactory.IssueSuperuser(Guid.NewGuid());
        var client = _factory.CreateOriginClient().WithBearer(bearerToken);
        _logs.Clear();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/content-moderation/foods/{Guid.NewGuid()}/block",
            new { reason_code = "malicious_content" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.AdministrativeContext);
        var moderation = Assert.Single(_logs.Snapshot(), log =>
            log.EventId == SecurityLogEvents.Moderation);
        Assert.Equal("rejected", moderation.State["SecurityOutcome"]);
        AssertLogsDoNotContainSentinels(_logs.Snapshot(), bearerToken);
    }

    private static void AssertLogsDoNotContainSentinels(
        IEnumerable<CapturedLogEntry> entries,
        params string[] additionalSentinels)
    {
        foreach (var entry in entries)
        {
            var structuredState = string.Join('|', entry.State.Select(pair => $"{pair.Key}={pair.Value}"));
            var exception = entry.Exception?.ToString() ?? string.Empty;

            foreach (var sentinel in new[] { EmailSentinel, PasswordSentinel }
                .Concat(additionalSentinels))
            {
                Assert.DoesNotContain(sentinel, entry.FormattedMessage, StringComparison.Ordinal);
                Assert.DoesNotContain(sentinel, structuredState, StringComparison.Ordinal);
                Assert.DoesNotContain(sentinel, exception, StringComparison.Ordinal);
            }
        }
    }
}
