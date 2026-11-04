using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

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
    private const string ChangePasswordRoute = "/api/v1/native/auth/change-password";
    private const string PasswordResetRequestRoute = "/api/v1/native/auth/password-reset/request";
    private const string PasswordResetCompleteRoute = "/api/v1/native/auth/password-reset/complete";

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
    public async Task Login_AccessTokenCarriesTheSignInTime()
    {
        var session = await RegisterNativeSessionAsync("native-auth-time");

        var authTime = AuthTimeOf(session.AccessToken);

        Assert.InRange(authTime, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Refresh_KeepsTheSignInTimeOfTheFamily_NotTheRefreshTime()
    {
        var first = await RegisterNativeSessionAsync("native-auth-time-refresh");
        var userId = new JsonWebToken(first.AccessToken).Subject;
        var signedInAt = DateTime.UtcNow.AddHours(-1);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
            await context.RefreshTokens
                .Where(token => token.UserId == Guid.Parse(userId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.CreatedAt, signedInAt), Token);
        }

        var rotated = await RefreshAsync(CreateNativeClient(), first.RefreshToken, first.CsrfToken);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var authTime = AuthTimeOf((await ReadSessionAsync(rotated)).AccessToken);
        Assert.InRange(authTime, new DateTimeOffset(signedInAt).AddSeconds(-2), new DateTimeOffset(signedInAt).AddSeconds(2));
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
    public async Task ChangePassword_FromTheApp_IsAcceptedWithoutOrigin_AndTheNewPasswordLogsIn()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "native-change-password");
        var session = await ReadSessionAsync(await RegisterAsync(CreateNativeClient(), invitation.RawToken));
        const string newPassword = "Native-Password-2!";

        var response = await ApiJsonPayload.PostAsync(
            CreateNativeClient().WithBearer(session.AccessToken),
            ChangePasswordRoute,
            new { currentPassword = AuthTestData.Password, newPassword, confirmNewPassword = newPassword },
            Token);
        var login = await ApiJsonPayload.PostAsync(
            CreateNativeClient(), LoginRoute, new { email = invitation.Email, password = newPassword }, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutBearer_Is401()
    {
        var response = await ApiJsonPayload.PostAsync(
            CreateNativeClient(),
            ChangePasswordRoute,
            new { currentPassword = AuthTestData.Password, newPassword = "Native-Password-2!", confirmNewPassword = "Native-Password-2!" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PasswordResetRequest_FromTheApp_Is204WithoutOrigin()
    {
        // 204 sempre, exista ou não a conta: igual à rota web.
        var response = await ApiJsonPayload.PostAsync(
            CreateNativeClient(), PasswordResetRequestRoute, new { email = "nobody@example.test" }, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PasswordResetComplete_FromTheApp_ReachesTheHandlerWithoutOrigin()
    {
        var response = await ApiJsonPayload.PostAsync(
            CreateNativeClient(),
            PasswordResetCompleteRoute,
            new { token = new string('a', 43), newPassword = "Native-Password-2!", confirmNewPassword = "Native-Password-2!" },
            Token);

        // Token inválido: responde o handler (401 com código), não o filtro de Origin (403).
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_password_reset_invalid", await TitleOf(response));
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

    private static DateTimeOffset AuthTimeOf(string accessToken) =>
        DateTimeOffset.FromUnixTimeSeconds(new JsonWebToken(accessToken).GetPayloadValue<long>("auth_time"));

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
