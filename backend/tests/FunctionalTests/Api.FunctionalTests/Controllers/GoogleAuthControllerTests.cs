using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Application.Features.Authentication.Google;
using Application.Features.Authentication.Google.Abstractions;
using Application.Results;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Contrato HTTP completo dos quatro endpoints Google. O verificador é substituído por
/// um duplo — nenhum teste contacta a Google nem usa um ID token real — mas todo o
/// resto do pipeline (Origin, rate limits, cookies, store transacional e PostgreSQL)
/// é o de produção.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class GoogleAuthControllerTests : IAsyncLifetime
{
    private const string ChallengeRoute = "/api/v1/auth/google/challenge";
    private const string SignInRoute = "/api/v1/auth/google/sign-in";
    private const string LinkChallengeRoute = "/api/v1/auth/google/link/challenge";
    private const string LinkRoute = "/api/v1/auth/google/link";
    private const string NonceCookie = "__Secure-ptm-google-nonce";
    private const string SeedPassword = "Functional-Password-1!";

    private static readonly DateTime SeedInstant =
        new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresApiFixture _database;
    private readonly StubExternalIdentityVerifier _verifier = new();
    private ApiWebApplicationFactory _factory = null!;

    public GoogleAuthControllerTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        // Factory própria: só assim se pode substituir IExternalIdentityVerifier sem
        // afetar as restantes classes que partilham o fixture e a mesma base migrada.
        _factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            ConfigureServices = services =>
                services.AddScoped<IExternalIdentityVerifier>(_ => _verifier)
        };
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Challenge_WithAllowedOrigin_ReturnsNonceAndSecureCookie()
    {
        var client = _factory.CreateOriginClient();

        var response = await client.PostAsync(
            ChallengeRoute, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var nonce = body.GetProperty("nonce").GetString();
        Assert.False(string.IsNullOrWhiteSpace(nonce));
        Assert.Equal(JsonValueKind.String, body.GetProperty("expires_at").ValueKind);

        var cookie = SetCookie(response, NonceCookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth/google", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nonce!, cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Challenge_PersistsOnlyTheNonceHash()
    {
        var client = _factory.CreateOriginClient();

        var response = await client.PostAsync(
            ChallengeRoute, null, TestContext.Current.CancellationToken);
        var nonce = (await ReadJsonAsync(response)).GetProperty("nonce").GetString()!;

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var hashes = await context.Set<ExternalAuthenticationChallenge>()
            .AsNoTracking()
            .Select(challenge => challenge.NonceHash)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(hashes, value => value == nonce);
    }

    [Theory]
    [InlineData(ChallengeRoute)]
    [InlineData(SignInRoute)]
    public async Task AnonymousRoute_WithoutOrigin_IsRejected(string route)
    {
        var client = CreateOriginlessClient();

        var response = await client.PostAsync(
            route, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(LinkChallengeRoute)]
    [InlineData(LinkRoute)]
    public async Task AuthenticatedRoute_WithoutOrigin_IsRejected(string route)
    {
        // Com JWT válido e sem Origin o pedido morre no RequireOriginFilter. Sem JWT
        // o 401 do middleware de autorização chega primeiro — a ordem do pipeline é
        // UseAuthorization antes dos filtros MVC —, pelo que o token é necessário
        // para provar que é o Origin a bloquear.
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, "google-origin", TestContext.Current.CancellationToken);
        var client = CreateOriginlessClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.PostAsync(
            route, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(ChallengeRoute)]
    [InlineData(SignInRoute)]
    public async Task AnonymousRoute_WithForeignOrigin_IsRejected(string route)
    {
        var client = CreateOriginlessClient();
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.test");

        var response = await client.PostAsync(
            route, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(LinkChallengeRoute)]
    [InlineData(LinkRoute)]
    public async Task LinkRoute_Anonymous_IsRejectedBeforeHandler(string route)
    {
        var client = _factory.CreateOriginClient();

        var response = await client.PostAsync(
            route, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _verifier.Calls);
    }

    [Fact]
    public async Task SignIn_ReturningTrainer_ReturnsSessionWithRefreshOnlyInCookie()
    {
        var seed = await SeedGoogleUserAsync("returning", "trainer");
        _verifier.Result = Verified(seed.Subject, seed.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("access_token").GetString()));
        Assert.Equal("trainer", body.GetProperty("role").GetString());

        var raw = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain("refresh_token", raw, StringComparison.OrdinalIgnoreCase);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, cookie =>
            cookie.Contains("ptm-refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SignIn_ConsumesChallengeSoTheSameCookieCannotBeReplayed()
    {
        var seed = await SeedGoogleUserAsync("replay", "trainer");
        _verifier.Result = Verified(seed.Subject, seed.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var first = await PostSignInAsync(client, nonce);
        // O cookie foi apagado pela resposta anterior; reapresentá-lo à mão prova que a
        // defesa está no challenge persistido e não apenas no browser.
        var second = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task SignIn_NewTrainerWithExternalDomain_ReturnsPendingWithoutSession()
    {
        _verifier.Result = Verified(
            $"sub-{Guid.NewGuid():N}",
            $"pending-{Guid.NewGuid():N}@empresa.test",
            isAuthoritative: false);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("email_confirmation_required", body.GetProperty("status").GetString());
        Assert.DoesNotContain(
            response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? cookies
                : [],
            cookie => cookie.Contains("ptm-refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SignIn_NewTrainerWithAuthoritativeEmail_ReturnsSessionAndCreatesTenant()
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        var email = $"new-{Guid.NewGuid():N}@gmail.com";
        _verifier.Result = Verified(subject, email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(email).Normalized;
        var user = await context.Users.AsNoTracking().SingleAsync(
            candidate => candidate.NormalizedEmail == normalized,
            TestContext.Current.CancellationToken);

        Assert.Equal("trainer", user.Role);
        Assert.True(user.EmailConfirmed);
        // A conta criada por Google nunca tem password local.
        Assert.Null(user.PasswordHash);
        // TrainerSettings e TrainerSubscription têm query filter por tenant: só são
        // visíveis num scope onde o próprio trainer é o tenant efetivo.
        await using var tenantScope = TenantScope(user.Id);
        var tenantContext = tenantScope.ServiceProvider
            .GetRequiredService<PtManagerDbContext>();
        Assert.True(await tenantContext.TrainerSettings.AsNoTracking().AnyAsync(
            settings => settings.TrainerId == user.Id,
            TestContext.Current.CancellationToken));
        Assert.True(await tenantContext.TrainerSubscriptions.AsNoTracking().AnyAsync(
            subscription => subscription.TrainerId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SignIn_ExistingLocalEmailWithUnknownSubject_RequiresExplicitLink()
    {
        // Regra central: o email nunca liga contas automaticamente.
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, "google-collision", TestContext.Current.CancellationToken);
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", trainer.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("google_account_link_required", body.GetProperty("title").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        Assert.False(await context.Set<ExternalIdentity>().AsNoTracking().AnyAsync(
            identity => identity.UserId == trainer.TrainerId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SignIn_InvalidCredential_ReturnsUnauthorizedWithoutClaims()
    {
        _verifier.Result = Result<VerifiedExternalIdentity>.Failure(
            GoogleAuthenticationErrors.InvalidCredential);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain("access_token", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sub", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_WithoutChallengeCookie_ReturnsProblemDetails()
    {
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", "orphan@gmail.com");
        var client = _factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            SignInRoute,
            new { id_token = "opaque", invitation_token = (string?)null },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_NewClientWithValidInvitation_BindsInvitationTenant()
    {
        var invitation = await SeedInvitationAsync("invited");
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", invitation.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, invitation.RawToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("client", body.GetProperty("role").GetString());
        Assert.Equal(
            invitation.TrainerId.ToString(),
            body.GetProperty("trainer_id").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var stored = await context.InviteTokens.AsNoTracking().SingleAsync(
            token => token.Id == invitation.InvitationId,
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored.UsedAt);
    }

    [Fact]
    public async Task SignIn_InvitationWithDifferentEmail_IsRejectedWithoutCreatingUser()
    {
        var invitation = await SeedInvitationAsync("mismatch");
        var googleEmail = $"other-{Guid.NewGuid():N}@gmail.com";
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", googleEmail);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, invitation.RawToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(
            "authentication_invitation_email_mismatch",
            body.GetProperty("title").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(googleEmail).Normalized;
        Assert.False(await context.Users.AsNoTracking().AnyAsync(
            user => user.NormalizedEmail == normalized,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SignIn_UnknownInvitation_IsRejected()
    {
        _verifier.Result = Verified(
            $"sub-{Guid.NewGuid():N}", $"unknown-{Guid.NewGuid():N}@gmail.com");
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, "not-a-real-invitation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("authentication_invitation_invalid", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SignIn_PendingTrainer_SendsConfirmationEmailToTheGoogleAddress()
    {
        var email = $"pending-mail-{Guid.NewGuid():N}@empresa.test";
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", email, isAuthoritative: false);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);
        _factory.EmailRequests.Clear();

        var response = await PostSignInAsync(client, nonce);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var sent = Assert.Single(_factory.EmailRequests.Requests);
        Assert.Contains(email, sent.Body, StringComparison.OrdinalIgnoreCase);

        // A conta fica criada mas por confirmar, e sem sessao emitida.
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(email).Normalized;
        var user = await context.Users.AsNoTracking().SingleAsync(
            candidate => candidate.NormalizedEmail == normalized,
            TestContext.Current.CancellationToken);
        Assert.False(user.EmailConfirmed);
        Assert.False(await context.RefreshTokens.AsNoTracking().AnyAsync(
            token => token.UserId == user.Id,
            TestContext.Current.CancellationToken));
        Assert.True(await context.EmailVerificationTokens.AsNoTracking().AnyAsync(
            token => token.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SignIn_ConsumedInvitation_IsRejectedWithoutCreatingUser()
    {
        var invitation = await SeedInvitationAsync("consumed", markUsed: true);
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", invitation.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, invitation.RawToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(
            "authentication_invitation_consumed",
            body.GetProperty("title").GetString());
        await AssertNoUserAsync(invitation.Email);
    }

    [Fact]
    public async Task SignIn_ExpiredInvitation_IsRejectedWithoutCreatingUser()
    {
        var invitation = await SeedInvitationAsync(
            "expired", expiresAt: DateTime.UtcNow.AddSeconds(2));
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", invitation.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        var response = await PostSignInAsync(client, nonce, invitation.RawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(
            "authentication_invitation_expired",
            body.GetProperty("title").GetString());
        await AssertNoUserAsync(invitation.Email);
    }

    [Fact]
    public async Task SignIn_InvitationForClientAlreadyAttached_IsRejected()
    {
        // A ficha do cliente ja tem conta: o convite nao pode criar uma segunda.
        var invitation = await SeedInvitationAsync("attached", attachUser: true);
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", invitation.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, invitation.RawToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(
            "authentication_relationship_conflict",
            body.GetProperty("title").GetString());
        await AssertNoUserAsync(invitation.Email);
    }

    [Fact]
    public async Task SignIn_KnownGoogleAccount_IgnoresInvitationToken()
    {
        // Uma conta Google ja conhecida entra pela identidade; transferencias de
        // relacao continuam a pertencer ao endpoint accept-invite.
        var seed = await SeedGoogleUserAsync("known-with-invite", "trainer");
        var invitation = await SeedInvitationAsync("ignored");
        _verifier.Result = Verified(seed.Subject, seed.Email);
        var client = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(client);

        var response = await PostSignInAsync(client, nonce, invitation.RawToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("trainer", body.GetProperty("role").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var stored = await context.InviteTokens.AsNoTracking().SingleAsync(
            token => token.Id == invitation.InvitationId,
            TestContext.Current.CancellationToken);
        Assert.Null(stored.UsedAt);
    }

    private async Task AssertNoUserAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(email).Normalized;
        Assert.False(await context.Users.AsNoTracking().AnyAsync(
            user => user.NormalizedEmail == normalized,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LinkChallenge_Authenticated_BindsChallengeToTokenSubject()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, "google-link-challenge", TestContext.Current.CancellationToken);
        var client = AuthenticatedTrainer(trainer.TrainerId);

        var response = await client.PostAsync(
            LinkChallengeRoute, null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        Assert.True(await context.Set<ExternalAuthenticationChallenge>().AsNoTracking().AnyAsync(
            challenge => challenge.UserId == trainer.TrainerId &&
                challenge.Purpose == ExternalAuthenticationChallenge.LinkPurpose,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Link_WithCorrectPasswordAndMatchingEmail_ReturnsNoContent()
    {
        var trainer = await SeedPasswordTrainerAsync("google-link-ok");
        var client = AuthenticatedTrainer(trainer.UserId);
        var nonce = await IssueLinkChallengeAsync(client);
        var subject = $"sub-{Guid.NewGuid():N}";
        _verifier.Result = Verified(subject, trainer.Email);

        var response = await PostLinkAsync(client, nonce, SeedPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie") &&
            response.Headers.GetValues("Set-Cookie").Any(cookie =>
                cookie.Contains("ptm-refresh", StringComparison.OrdinalIgnoreCase)));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        Assert.True(await context.Set<ExternalIdentity>().AsNoTracking().AnyAsync(
            identity => identity.UserId == trainer.UserId && identity.Subject == subject,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Link_WithWrongPassword_IsRejectedAndCreatesNoIdentity()
    {
        var trainer = await SeedPasswordTrainerAsync("google-link-badpass");
        var client = AuthenticatedTrainer(trainer.UserId);
        var nonce = await IssueLinkChallengeAsync(client);
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", trainer.Email);

        var response = await PostLinkAsync(client, nonce, "Wrong-Password-9!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoIdentityAsync(trainer.UserId);
    }

    [Fact]
    public async Task Link_WithDifferentGoogleEmail_IsRejectedAndCreatesNoIdentity()
    {
        var trainer = await SeedPasswordTrainerAsync("google-link-otheremail");
        var client = AuthenticatedTrainer(trainer.UserId);
        var nonce = await IssueLinkChallengeAsync(client);
        _verifier.Result = Verified(
            $"sub-{Guid.NewGuid():N}", $"different-{Guid.NewGuid():N}@gmail.com");

        var response = await PostLinkAsync(client, nonce, SeedPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("google_link_email_mismatch", body.GetProperty("title").GetString());
        await AssertNoIdentityAsync(trainer.UserId);
    }

    [Fact]
    public async Task Link_WithChallengeIssuedForAnotherUser_IsRejected()
    {
        // O challenge de link está preso ao UserId que o pediu: reutilizá-lo noutra
        // sessão autenticada não pode ligar a identidade Google.
        var owner = await SeedPasswordTrainerAsync("google-link-owner");
        var attacker = await SeedPasswordTrainerAsync("google-link-attacker");
        var ownerClient = AuthenticatedTrainer(owner.UserId);
        var nonce = await IssueLinkChallengeAsync(ownerClient);
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", attacker.Email);

        var attackerClient = AuthenticatedTrainer(attacker.UserId);
        var response = await PostLinkAsync(attackerClient, nonce, SeedPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoIdentityAsync(attacker.UserId);
    }

    [Fact]
    public async Task Link_WithSignInChallenge_IsRejected()
    {
        // Um nonce emitido para sign-in não serve para linking: o purpose é verificado.
        var trainer = await SeedPasswordTrainerAsync("google-link-purpose");
        var anonymous = _factory.CreateOriginClient();
        var nonce = await IssueChallengeAsync(anonymous);
        var client = AuthenticatedTrainer(trainer.UserId);
        _verifier.Result = Verified($"sub-{Guid.NewGuid():N}", trainer.Email);

        var response = await PostLinkAsync(client, nonce, SeedPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoIdentityAsync(trainer.UserId);
    }

    [Fact]
    public async Task Link_AlreadyLinkedSubject_ReturnsConflict()
    {
        var existing = await SeedGoogleUserAsync("google-link-taken", "trainer");
        var trainer = await SeedPasswordTrainerAsync("google-link-second");
        var client = AuthenticatedTrainer(trainer.UserId);
        var nonce = await IssueLinkChallengeAsync(client);
        _verifier.Result = Verified(existing.Subject, trainer.Email);

        var response = await PostLinkAsync(client, nonce, SeedPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("google_identity_conflict", body.GetProperty("title").GetString());
        await AssertNoIdentityAsync(trainer.UserId);
    }

    private async Task AssertNoIdentityAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        Assert.False(await context.Set<ExternalIdentity>().AsNoTracking().AnyAsync(
            identity => identity.UserId == userId,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Cliente sem header Origin. O BaseAddress tem de ser https: em http o
    /// UseHttpsRedirection responderia antes de qualquer filtro e o teste passaria
    /// a medir o redirect em vez do controlo em prova.
    /// </summary>
    private HttpClient CreateOriginlessClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    private HttpClient AuthenticatedTrainer(Guid userId)
    {
        var client = _factory.CreateOriginClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.IssueTrainer(userId));
        return client;
    }

    private async Task<string> IssueChallengeAsync(HttpClient client)
    {
        var response = await client.PostAsync(
            ChallengeRoute, null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("nonce").GetString()!;
    }

    private async Task<string> IssueLinkChallengeAsync(HttpClient client)
    {
        var response = await client.PostAsync(
            LinkChallengeRoute, null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("nonce").GetString()!;
    }

    private static Task<HttpResponseMessage> PostSignInAsync(
        HttpClient client,
        string nonce,
        string? invitationToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, SignInRoute)
        {
            Content = JsonContent.Create(
                new { id_token = "opaque-id-token", invitation_token = invitationToken },
                options: ApiJsonPayload.Options)
        };
        request.Headers.Add("Cookie", $"{NonceCookie}={nonce}");
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> PostLinkAsync(
        HttpClient client,
        string nonce,
        string currentPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LinkRoute)
        {
            Content = JsonContent.Create(
                new { id_token = "opaque-id-token", current_password = currentPassword },
                options: ApiJsonPayload.Options)
        };
        request.Headers.Add("Cookie", $"{NonceCookie}={nonce}");
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Result<VerifiedExternalIdentity> Verified(
        string subject,
        string email,
        bool isAuthoritative = true) =>
        Result<VerifiedExternalIdentity>.Success(new VerifiedExternalIdentity(
            ExternalIdentity.GoogleProvider, subject, email, "Google User", isAuthoritative));

    /// <summary>
    /// Abre um scope com o tenant estabelecido, tal como o pipeline HTTP faz antes de
    /// qualquer escrita. Semear sem tenant produziria estado que a aplicação real nunca
    /// conseguiria criar.
    /// </summary>
    private AsyncServiceScope TenantScope(Guid trainerId)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<Application.Common.Abstractions.ITenantContextInitializer>()
            .Establish(
                trainerId,
                trainerId,
                "trainer",
                Application.Common.Abstractions.TenantOrigin.System,
                false);
        return scope;
    }

    private async Task<GoogleSeed> SeedGoogleUserAsync(string discriminator, string role)
    {
        var email = $"{discriminator}-{Guid.NewGuid():N}@gmail.com";
        var subject = $"sub-{Guid.NewGuid():N}";
        var user = new User(new EmailAddress(email), role, "Google User", SeedInstant);
        user.ConfirmEmail(SeedInstant);

        // TrainerSettings e TrainerSubscription exigem tenant efetivo no interceptor,
        // tal como em produção: o trainer é a raiz do seu próprio tenant.
        await using var scope = TenantScope(user.Id);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        context.Users.Add(user);
        context.Set<ExternalIdentity>().Add(new ExternalIdentity(
            user.Id, ExternalIdentity.GoogleProvider, subject, SeedInstant));
        if (role == "trainer")
        {
            context.TrainerSettings.Add(
                new Domain.Entities.TrainerSettings.TrainerSettings(user.Id, SeedInstant));
            context.TrainerSubscriptions.Add(new Domain.Entities.Billing.TrainerSubscription(
                user.Id, SeedInstant.AddDays(15), SeedInstant));
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new GoogleSeed(user.Id, email, subject);
    }

    private async Task<PasswordTrainerSeed> SeedPasswordTrainerAsync(string discriminator)
    {
        var seeded = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, discriminator, TestContext.Current.CancellationToken);

        await using var scope = TenantScope(seeded.TrainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var user = await context.Users.SingleAsync(
            candidate => candidate.Id == seeded.TrainerId,
            TestContext.Current.CancellationToken);
        // A password é definida pelo mesmo hasher usado em produção, para que
        // CheckPasswordAsync no store real percorra o caminho verdadeiro.
        var hasher = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        user.SetPasswordHash(hasher.HashPassword(user, SeedPassword), SeedInstant);
        user.ConfirmEmail(SeedInstant);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new PasswordTrainerSeed(seeded.TrainerId, seeded.Email);
    }

    private async Task<InvitationSeed> SeedInvitationAsync(
        string discriminator,
        bool markUsed = false,
        bool attachUser = false,
        DateTime? expiresAt = null)
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, discriminator, TestContext.Current.CancellationToken);
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            _factory, trainer.TrainerId, "Invited Client",
            TestContext.Current.CancellationToken);

        await using var scope = TenantScope(trainer.TrainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        // O store compara o email do convite com o email Google; a ficha do cliente
        // não participa nessa decisão, pelo que fica como o seeder a criou.
        var email = $"{discriminator}-client-{Guid.NewGuid():N}@gmail.com";

        var tokens = scope.ServiceProvider
            .GetRequiredService<Application.Features.Authentication.Abstractions.IOpaqueTokenService>();
        var generated = tokens.Generate();
        var invitation = new InviteToken(
            trainer.TrainerId,
            clientId,
            new EmailAddress(email),
            generated.TokenHash,
            expiresAt ?? SeedInstant.AddDays(7),
            expiresAt.HasValue ? expiresAt.Value.AddSeconds(-1) : SeedInstant);
        if (markUsed)
            invitation.MarkUsed(SeedInstant.AddMinutes(1));

        context.InviteTokens.Add(invitation);

        if (attachUser)
        {
            var existing = new User(
                new EmailAddress($"attached-{Guid.NewGuid():N}@example.test"),
                "client",
                "Attached Client",
                SeedInstant);
            existing.ConfirmEmail(SeedInstant);
            context.Users.Add(existing);
            var clientRow = await context.Clients.SingleAsync(
                candidate => candidate.Id == clientId,
                TestContext.Current.CancellationToken);
            clientRow.AttachUser(existing.Id, SeedInstant);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new InvitationSeed(
            trainer.TrainerId, invitation.Id, email, generated.RawToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken)).RootElement.Clone();

    private static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(cookie =>
            cookie.StartsWith($"{name}=", StringComparison.Ordinal));

    private sealed record GoogleSeed(Guid UserId, string Email, string Subject);

    private sealed record PasswordTrainerSeed(Guid UserId, string Email);

    private sealed record InvitationSeed(
        Guid TrainerId,
        Guid InvitationId,
        string Email,
        string RawToken);

    /// <summary>
    /// Substitui apenas a verificação criptográfica da credencial Google. Todo o
    /// restante pipeline — Origin, rate limits, cookies, store e PostgreSQL — é real.
    /// </summary>
    private sealed class StubExternalIdentityVerifier : IExternalIdentityVerifier
    {
        internal Result<VerifiedExternalIdentity> Result { get; set; } =
            Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential);

        internal int Calls { get; private set; }

        public Task<Result<VerifiedExternalIdentity>> VerifyAsync(
            string provider,
            string idToken,
            string expectedNonce,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
