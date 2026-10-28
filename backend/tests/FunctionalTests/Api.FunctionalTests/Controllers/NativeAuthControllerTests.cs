using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Sessão da app nativa: o refresh token vai e volta no corpo, nunca há cookie, e a
/// rotação, o reuso e o logout comportam-se como na sessão web.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class NativeAuthControllerTests : IAsyncLifetime
{
    private const string RegisterRoute = "/api/v1/native/auth/accept-invite/register";
    private const string LoginRoute = "/api/v1/native/auth/login";
    private const string RefreshRoute = "/api/v1/native/auth/refresh";
    private const string LogoutRoute = "/api/v1/native/auth/logout";
    private const string WebLoginRoute = "/api/v1/auth/login";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public NativeAuthControllerTests(PostgresApiFixture database) => _database = database;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
    public async Task Register_WithoutOrigin_ReturnsRefreshTokenInBodyAndNoCookie()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "native-register");
        var app = CreateNativeClient();

        var response = await RegisterAsync(app, invitation.RawToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
        var session = await ReadSessionAsync(response);
        Assert.Equal("client", session.Role);
        Assert.Equal(invitation.TrainerId, session.TrainerId);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
        Assert.True(session.RefreshTokenExpiresAt > DateTime.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task Login_WithoutOrigin_ReturnsTokensAndNoCookie()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "native-login");
        var app = CreateNativeClient();
        await RegisterAsync(app, invitation.RawToken);

        var response = await ApiJsonPayload.PostAsync(
            app, LoginRoute, new { email = invitation.Email, password = AuthTestData.Password }, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var session = await ReadSessionAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
    }

    [Fact]
    public async Task Refresh_RotatesTheToken_AndReusingTheOldOneRevokesTheFamily()
    {
        var first = await RegisterNativeSessionAsync("native-rotate");
        var app = CreateNativeClient();

        var rotated = await RefreshAsync(app, first.RefreshToken, first.CsrfToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.False(rotated.Headers.Contains("Set-Cookie"));
        var second = await ReadSessionAsync(rotated);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var reused = await RefreshAsync(app, first.RefreshToken, first.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal("authentication_refresh_session_invalid", await TitleOf(reused));

        // O reuso revogou a família: o token novo, legítimo até aqui, também morreu.
        var afterReuse = await RefreshAsync(app, second.RefreshToken, second.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WrongCsrf_Is403AndKeepsTheSession()
    {
        var session = await RegisterNativeSessionAsync("native-csrf");
        var app = CreateNativeClient();

        var rejected = await RefreshAsync(app, session.RefreshToken, new string('x', 43));
        var accepted = await RefreshAsync(app, session.RefreshToken, session.CsrfToken);

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("authentication_csrf_token_invalid", await TitleOf(rejected));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Refresh_NeverReadsTheWebCookie()
    {
        // Um browser com a sessão web aberta chama a rota nativa sem token no corpo: o
        // cookie existe no cliente, mas a rota nativa nunca o lê.
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "native-cookie");
        var browser = _factory.CreateOriginClient();
        var register = await ApiJsonPayload.PostAsync(
            browser,
            "/api/v1/auth/accept-invite/register",
            new
            {
                token = invitation.RawToken,
                password = AuthTestData.Password,
                confirmPassword = AuthTestData.Password
            },
            Token);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        using var web = JsonDocument.Parse(await register.Content.ReadAsStringAsync(Token));
        var csrf = web.RootElement.GetProperty("csrf_token").GetString()!;

        var response = await RefreshAsync(browser, string.Empty, csrf);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Logout_RevokesTheSession_AndIsIdempotent()
    {
        var session = await RegisterNativeSessionAsync("native-logout");
        var app = CreateNativeClient();

        var logout = await ApiJsonPayload.PostAsync(
            app, LogoutRoute, new { refreshToken = session.RefreshToken, csrfToken = session.CsrfToken }, Token);
        var again = await ApiJsonPayload.PostAsync(
            app, LogoutRoute, new { refreshToken = session.RefreshToken, csrfToken = session.CsrfToken }, Token);
        var refresh = await RefreshAsync(app, session.RefreshToken, session.CsrfToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.False(logout.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task WebLogin_WithoutOrigin_StillIs403()
    {
        // A rota nativa não abre exceções no caminho web: sem Origin continua a ser recusado.
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "web-no-origin");
        var app = CreateNativeClient();
        await RegisterAsync(app, invitation.RawToken);

        var response = await ApiJsonPayload.PostAsync(
            app, WebLoginRoute, new { email = invitation.Email, password = AuthTestData.Password }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private HttpClient CreateNativeClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<NativeSession> RegisterNativeSessionAsync(string prefix)
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, prefix);
        var response = await RegisterAsync(CreateNativeClient(), invitation.RawToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadSessionAsync(response);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string token) =>
        ApiJsonPayload.PostAsync(
            client,
            RegisterRoute,
            new { token, password = AuthTestData.Password, confirmPassword = AuthTestData.Password },
            Token);

    private static Task<HttpResponseMessage> RefreshAsync(
        HttpClient client, string refreshToken, string csrfToken) =>
        ApiJsonPayload.PostAsync(client, RefreshRoute, new { refreshToken, csrfToken }, Token);

    private static async Task<NativeSession> ReadSessionAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var root = body.RootElement;
        return new NativeSession(
            root.GetProperty("role").GetString()!,
            root.GetProperty("trainer_id").GetGuid(),
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("refresh_token").GetString()!,
            root.GetProperty("refresh_token_expires_at").GetDateTime(),
            root.GetProperty("csrf_token").GetString()!);
    }

    private static async Task<string?> TitleOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return body.RootElement.GetProperty("title").GetString();
    }

    private sealed record NativeSession(
        string Role,
        Guid TrainerId,
        string AccessToken,
        string RefreshToken,
        DateTime RefreshTokenExpiresAt,
        string CsrfToken);
}
