using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Application.Features.Authentication.Google;
using Application.Features.Authentication.Google.Abstractions;
using Application.Results;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Security;

/// <summary>
/// Controlos de segurança transversais do fluxo Google: limite de tentativas por IP,
/// consumo atómico do nonce sob concorrência e ausência de segredos nos logs.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class GoogleAuthenticationSecurityTests : IAsyncLifetime
{
    private const string ChallengeRoute = "/api/v1/auth/google/challenge";
    private const string SignInRoute = "/api/v1/auth/google/sign-in";
    private const string NonceCookie = "__Secure-ptm-google-nonce";

    private static readonly DateTime SeedInstant =
        new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresApiFixture _database;
    private readonly StubVerifier _verifier = new();
    private readonly CapturingLoggerProvider _logs = new();
    private ApiWebApplicationFactory _factory = null!;

    public GoogleAuthenticationSecurityTests(PostgresApiFixture database) =>
        _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            ConfigureServices = services =>
            {
                services.AddScoped<IExternalIdentityVerifier>(_ => _verifier);
                services.AddSingleton<ILoggerProvider>(_logs);
            }
        };
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Challenge_ElevenAttemptsFromSameIp_IsThrottled()
    {
        // A política auth_google_sign_in permite dez pedidos por minuto e por IP.
        var client = _factory.CreateOriginClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 11; attempt++)
        {
            var response = await client.PostAsync(
                ChallengeRoute, null, TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.True(
                    response.Headers.RetryAfter is not null ||
                    response.Headers.Contains("Retry-After"),
                    "A 429 has to tell the caller when to retry.");
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Equal(10, statuses.Count(status => status == HttpStatusCode.OK));
    }

    [Fact]
    public async Task SignIn_TwoConcurrentRequestsWithSameNonce_AuthenticateExactlyOnce()
    {
        // O nonce é consumido sob lock PostgreSQL: duas corridas simultâneas com o mesmo
        // challenge não podem produzir duas sessões.
        var seed = await SeedGoogleTrainerAsync();
        _verifier.Result = Verified(seed.Subject, seed.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var first = PostSignInAsync(client, nonce);
        var second = PostSignInAsync(client, nonce);
        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(response =>
            response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response =>
            response.StatusCode != HttpStatusCode.OK));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var sessions = await context.RefreshTokens.AsNoTracking().CountAsync(
            token => token.UserId == seed.UserId,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, sessions);
    }

    [Fact]
    public async Task SignIn_ConcurrentOnboardingOfSameSubject_CreatesExactlyOneAccount()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var email = $"concurrent-{Guid.NewGuid():N}@gmail.com";
        _verifier.Result = Verified(subject, email);
        var client = _factory.CreateOriginClient();
        var firstNonce = await IssueChallengeAsync(client);
        var secondNonce = await IssueChallengeAsync(client);

        var responses = await Task.WhenAll(
            PostSignInAsync(client, firstNonce),
            PostSignInAsync(client, secondNonce));

        Assert.Equal(1, responses.Count(response =>
            response.StatusCode == HttpStatusCode.OK));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(email).Normalized;
        Assert.Equal(1, await context.Users.AsNoTracking().CountAsync(
            user => user.NormalizedEmail == normalized,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.Set<ExternalIdentity>().AsNoTracking().CountAsync(
            identity => identity.Subject == subject,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SignIn_NeverWritesCredentialsOrNoncesToLogs()
    {
        const string idTokenSentinel = "SENTINEL-ID-TOKEN-8f2b1c";
        const string invitationSentinel = "SENTINEL-INVITATION-4a9e7d";
        var seed = await SeedGoogleTrainerAsync();
        _verifier.Result = Verified(seed.Subject, seed.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);
        _logs.Clear();

        await PostSignInAsync(client, nonce, idTokenSentinel, invitationSentinel);

        var captured = _logs.Snapshot();
        Assert.DoesNotContain(captured, entry =>
            entry.Contains(idTokenSentinel, StringComparison.Ordinal));
        Assert.DoesNotContain(captured, entry =>
            entry.Contains(invitationSentinel, StringComparison.Ordinal));
        Assert.DoesNotContain(captured, entry =>
            entry.Contains(nonce, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Challenge_MirrorsTheAuthCookieSameSitePolicyOfTheEnvironment()
    {
        // O cookie do nonce nao pode ter uma politica mais fraca do que a do cookie
        // Auth do mesmo ambiente: ambos participam do mesmo fluxo de sessao.
        using var factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            ConfigureServices = services =>
                services.AddScoped<IExternalIdentityVerifier>(_ => _verifier)
        };
        var client = factory.CreateOriginClient();

        var response = await client.PostAsync(
            ChallengeRoute, null, TestContext.Current.CancellationToken);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith($"{NonceCookie}=", StringComparison.Ordinal));
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("__Secure-", NonceCookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ThroughTheRealVerifier_RejectsAForgedIdToken()
    {
        // Sem substituir o adapter, um token assinado por outra chave tem de morrer na
        // verificacao criptografica do Google.Apis.Auth, antes de tocar na base de dados.
        using var factory = new ApiWebApplicationFactory(_database.ConnectionString);
        var client = factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, ForgedIdToken());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        Assert.False(await context.Set<ExternalIdentity>().AsNoTracking().AnyAsync(
            identity => identity.Subject == "forged-subject",
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Constroi um JWT com a forma certa e uma assinatura que a Google nunca emitiu.
    /// Serve para provar que a validacao e criptografica e nao estrutural.
    /// </summary>
    private static string ForgedIdToken()
    {
        var header = Base64Url("{\"alg\":\"RS256\",\"kid\":\"forged\",\"typ\":\"JWT\"}");
        var payload = Base64Url(
            "{\"iss\":\"https://accounts.google.com\",\"sub\":\"forged-subject\","
            + "\"email\":\"forged@gmail.com\",\"email_verified\":true,"
            + "\"aud\":\"ptmanager-tests.apps.googleusercontent.com\",\"exp\":4102444800}");
        var signature = Base64Url("not-a-real-google-signature");
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64Url(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private async Task<GoogleSeed> SeedGoogleTrainerAsync()
    {
        var email = $"security-{Guid.NewGuid():N}@gmail.com";
        var subject = $"sub-{Guid.NewGuid():N}";
        var user = new User(new EmailAddress(email), "trainer", "Google User", SeedInstant);
        user.ConfirmEmail(SeedInstant);

        var scope = _factory.Services.CreateAsyncScope();
        await using (scope)
        {
            scope.ServiceProvider
                .GetRequiredService<Application.Common.Abstractions.ITenantContextInitializer>()
                .Establish(
                    user.Id,
                    user.Id,
                    "trainer",
                    Application.Common.Abstractions.TenantOrigin.System,
                    false);

            var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
            context.Users.Add(user);
            context.Set<ExternalIdentity>().Add(new ExternalIdentity(
                user.Id, ExternalIdentity.GoogleProvider, subject, SeedInstant));
            context.TrainerSettings.Add(
                new Domain.Entities.TrainerSettings.TrainerSettings(user.Id, SeedInstant));
            context.TrainerSubscriptions.Add(
                new Domain.Entities.Billing.TrainerSubscription(
                    user.Id, SeedInstant.AddDays(15), SeedInstant));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new GoogleSeed(user.Id, email, subject);
    }

    private static async Task<string> IssueChallengeAsync(HttpClient client)
    {
        var response = await client.PostAsync(
            ChallengeRoute, null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("nonce").GetString()!;
    }

    private static Task<HttpResponseMessage> PostSignInAsync(
        HttpClient client,
        string nonce,
        string idToken = "opaque-id-token",
        string? invitationToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, SignInRoute)
        {
            Content = JsonContent.Create(
                new { id_token = idToken, invitation_token = invitationToken },
                options: ApiJsonPayload.Options)
        };
        request.Headers.Add("Cookie", $"{NonceCookie}={nonce}");
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Result<VerifiedExternalIdentity> Verified(string subject, string email) =>
        Result<VerifiedExternalIdentity>.Success(new VerifiedExternalIdentity(
            ExternalIdentity.GoogleProvider, subject, email, "Google User", true));

    private sealed record GoogleSeed(Guid UserId, string Email, string Subject);

    private sealed class StubVerifier : IExternalIdentityVerifier
    {
        internal Result<VerifiedExternalIdentity> Result { get; set; } =
            Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);

        public Task<Result<VerifiedExternalIdentity>> VerifyAsync(
            string provider,
            string idToken,
            string expectedNonce,
            CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    /// <summary>
    /// Captura mensagem formatada, estado estruturado e exceção de cada registo, que
    /// são os três sítios por onde um segredo poderia escapar para os logs.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _entries = [];
        private readonly Lock _gate = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Clear()
        {
            lock (_gate)
                _entries.Clear();
        }

        public IReadOnlyList<string> Snapshot()
        {
            lock (_gate)
                return [.. _entries];
        }

        private void Add(string entry)
        {
            lock (_gate)
                _entries.Add(entry);
        }

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                owner.Add(formatter(state, exception));
                owner.Add(state?.ToString() ?? string.Empty);
                if (exception is not null)
                    owner.Add(exception.ToString());
            }
        }
    }
}
