using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Sessão web: o refresh token vive só no cookie HttpOnly, e rodar ou terminar a sessão
/// exige Origin aprovada e o segredo anti-CSRF no cabeçalho.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthSessionCookieTests : IAsyncLifetime
{
    private const string RegisterRoute = "/api/v1/auth/accept-invite/register";
    private const string RefreshRoute = "/api/v1/auth/refresh";
    private const string LogoutRoute = "/api/v1/auth/logout";
    private const string CsrfHeader = "X-CSRF-Token";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthSessionCookieTests(PostgresApiFixture database) => _database = database;

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
    public async Task Refresh_CookieAndCsrf_RotatesAndKeepsTheRefreshTokenOutOfTheBody()
    {
        var (browser, csrf) = await OpenWebSessionAsync("web-refresh");

        var response = await PostWithCsrfAsync(browser, RefreshRoute, csrf);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("refresh_token", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("ptm-refresh", StringComparison.OrdinalIgnoreCase) &&
                      cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Refresh_CookieWithWrongCsrf_Is403()
    {
        var (browser, _) = await OpenWebSessionAsync("web-wrong-csrf");

        var response = await PostWithCsrfAsync(browser, RefreshRoute, new string('x', 43));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("authentication_csrf_token_invalid", await TitleOf(response));
    }

    [Fact]
    public async Task Refresh_CookieWithoutCsrf_IsRejected()
    {
        var (browser, _) = await OpenWebSessionAsync("web-no-csrf");

        var response = await browser.PostAsync(RefreshRoute, content: null, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutOrigin_Is403EvenWithCookieAndCsrf()
    {
        var (browser, csrf) = await OpenWebSessionAsync("web-no-origin");
        browser.DefaultRequestHeaders.Remove("Origin");

        var response = await PostWithCsrfAsync(browser, RefreshRoute, csrf);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_DeletesTheCookieAndTheSessionStopsRefreshing()
    {
        var (browser, csrf) = await OpenWebSessionAsync("web-logout");

        var logout = await PostWithCsrfAsync(browser, LogoutRoute, csrf);
        var refresh = await PostWithCsrfAsync(browser, RefreshRoute, csrf);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(
            logout.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("ptm-refresh", StringComparison.OrdinalIgnoreCase) &&
                      cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(HttpStatusCode.OK, refresh.StatusCode);
    }

    private async Task<(HttpClient Browser, string Csrf)> OpenWebSessionAsync(string prefix)
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, prefix);
        var browser = _factory.CreateOriginClient();
        var response = await ApiJsonPayload.PostAsync(
            browser,
            RegisterRoute,
            new
            {
                token = invitation.RawToken,
                password = AuthTestData.Password,
                confirmPassword = AuthTestData.Password
            },
            Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return (browser, body.RootElement.GetProperty("csrf_token").GetString()!);
    }

    private static Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client, string route, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Add(CsrfHeader, csrf);
        return client.SendAsync(request, Token);
    }

    private static async Task<string?> TitleOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return body.RootElement.GetProperty("title").GetString();
    }
}
