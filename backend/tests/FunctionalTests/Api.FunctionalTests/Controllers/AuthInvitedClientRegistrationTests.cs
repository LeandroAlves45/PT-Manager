using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// O convidado cria a conta e entra no mesmo passo: o link do convite prova o email e
/// o corpo só traz a password.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthInvitedClientRegistrationTests : IAsyncLifetime
{
    private const string RegisterRoute = "/api/v1/auth/accept-invite/register";
    private const string LoginRoute = "/api/v1/auth/login";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthInvitedClientRegistrationTests(PostgresApiFixture database) =>
        _database = database;

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
    public async Task Register_ValidInvite_ReturnsClientSessionWithRefreshOnlyInCookie()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "register-ok");
        var client = _factory.CreateOriginClient();

        var response = await RegisterAsync(client, invitation.RawToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("refresh_token", raw, StringComparison.OrdinalIgnoreCase);
        using var body = JsonDocument.Parse(raw);
        Assert.Equal("client", body.RootElement.GetProperty("role").GetString());
        Assert.Equal(
            invitation.TrainerId, body.RootElement.GetProperty("trainer_id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(
            body.RootElement.GetProperty("access_token").GetString()));
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("ptm-refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_ValidInvite_CreatesAConfirmedAccountThatCanLogInAfterwards()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "register-login");
        var client = _factory.CreateOriginClient();
        await RegisterAsync(client, invitation.RawToken);

        var login = await client.PostAsJsonAsync(
            LoginRoute,
            new { email = invitation.Email, password = AuthTestData.Password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(invitation.Email).Normalized;
        var user = await context.Users.AsNoTracking().SingleAsync(
            candidate => candidate.NormalizedEmail == normalized,
            TestContext.Current.CancellationToken);
        Assert.Equal(("client", true), (user.Role, user.EmailConfirmed));
        var attached = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(ficha => ficha.Id == invitation.ClientId)
            .Select(ficha => ficha.UserId)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(user.Id, attached);
    }

    [Fact]
    public async Task Register_SameInviteTwice_SecondIsConflict()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "register-twice");
        var client = _factory.CreateOriginClient();

        var first = await RegisterAsync(client, invitation.RawToken);
        var second = await RegisterAsync(client, invitation.RawToken);

        Assert.Equal(
            (HttpStatusCode.OK, HttpStatusCode.Conflict),
            (first.StatusCode, second.StatusCode));
        Assert.Equal("authentication_invitation_consumed", await TitleOf(second));
    }

    [Fact]
    public async Task Register_TwoConcurrentRequests_CreateExactlyOneAccount()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "register-race");
        var first = _factory.CreateOriginClient();
        var second = _factory.CreateOriginClient();

        var responses = await Task.WhenAll(
            RegisterAsync(first, invitation.RawToken),
            RegisterAsync(second, invitation.RawToken));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(invitation.Email).Normalized;
        Assert.Equal(1, await context.Users.AsNoTracking().CountAsync(
            user => user.NormalizedEmail == normalized, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Register_UnknownToken_Is401Invalid()
    {
        var client = _factory.CreateOriginClient();

        var response = await RegisterAsync(client, "no-such-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_invitation_invalid", await TitleOf(response));
    }

    [Fact]
    public async Task Register_ExpiredInvite_Is401Expired()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(
            _factory, "register-expired", expiresAt: DateTime.UtcNow.AddSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var client = _factory.CreateOriginClient();

        var response = await RegisterAsync(client, invitation.RawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_invitation_expired", await TitleOf(response));
    }

    [Fact]
    public async Task Register_EmailAlreadyHasAnAccount_Is409AndKeepsTheInviteUsable()
    {
        var email = $"has-account-{Guid.NewGuid():N}@client.test";
        await AuthTestData.SeedAccountAsync(_factory, email, role: "client");
        var invitation = await AuthTestData.SeedInvitationAsync(
            _factory, "register-exists", email: email);
        var client = _factory.CreateOriginClient();

        var response = await RegisterAsync(client, invitation.RawToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("authentication_invitation_account_exists", await TitleOf(response));
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        Assert.Null(await context.InviteTokens.AsNoTracking()
            .Where(token => token.Id == invitation.InviteId)
            .Select(token => token.UsedAt)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Register_WeakPassword_Is400AndLeavesTheInviteUsable()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "register-weak");
        var client = _factory.CreateOriginClient();

        var weak = await RegisterAsync(client, invitation.RawToken, "alllowercase");
        var strong = await RegisterAsync(client, invitation.RawToken);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        using var document = JsonDocument.Parse(
            await weak.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == "authentication_registration_rejected");
        Assert.Equal(HttpStatusCode.OK, strong.StatusCode);
    }

    [Theory]
    [InlineData("", "Functional-Password-1!", "Functional-Password-1!", "authentication_invite_token_required")]
    [InlineData("token", "short", "short", "authentication_password_too_short")]
    [InlineData("token", "Functional-Password-1!", "Different-Password-1!", "authentication_password_confirmation_mismatch")]
    public async Task Register_InvalidBody_Is400WithAStableCode(
        string token,
        string password,
        string confirmPassword,
        string code)
    {
        var client = _factory.CreateOriginClient();

        var response = await client.PostAsJsonAsync(
            RegisterRoute,
            new { token, password, confirm_password = confirmPassword },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task Register_WithoutOrigin_IsRejected()
    {
        var response = await RegisterAsync(_factory.CreateClient(), "token");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_EleventhRequestFromTheSameClient_IsRateLimited()
    {
        var client = _factory.CreateOriginClient();

        HttpStatusCode last = default;
        for (var attempt = 0; attempt < 11; attempt++)
            last = (await RegisterAsync(client, "no-such-token")).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string token,
        string password = AuthTestData.Password) =>
        client.PostAsJsonAsync(
            RegisterRoute,
            new { token, password, confirm_password = password },
            TestContext.Current.CancellationToken);

    private static async Task<string?> TitleOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("title").GetString();
    }
}
