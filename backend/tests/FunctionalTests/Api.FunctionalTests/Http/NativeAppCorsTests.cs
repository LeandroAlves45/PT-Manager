using System.Net;
using System.Text;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.FunctionalTests.Http;

/// <summary>
/// A app Capacitor chama a API pelo <c>fetch</c> da WebView, com a origem fixa da plataforma
/// (<c>capacitor://localhost</c> no iOS, <c>https://localhost</c> no Android). Estas origens
/// recebem CORS sem credenciais: os pedidos levam Bearer, nunca o cookie de refresh, e as
/// rotas de cookie da web continuam a recusá-las.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class NativeAppCorsTests
{
    private const string NativeLoginRoute = "/api/v1/native/auth/login";
    private const string WebLoginRoute = "/api/v1/auth/login";

    private readonly PostgresApiFixture _fixture;

    public NativeAppCorsTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("capacitor://localhost", NativeLoginRoute)]
    [InlineData("https://localhost", NativeLoginRoute)]
    [InlineData("capacitor://localhost", "/api/v1/portal/home")]
    [InlineData("https://localhost", "/api/v1/portal/home")]
    public async Task Preflight_NativeOrigin_IsAllowedWithoutCredentials(string origin, string route)
    {
        var response = await PreflightAsync(origin, route, "POST", "Authorization, Content-Type");

        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Content-Type")]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Correlation-ID")]
    public async Task Preflight_NativeOrigin_AllowsTheHeadersTheAppSends(string header)
    {
        var response = await PreflightAsync("capacitor://localhost", NativeLoginRoute, "POST", header);

        var allowed = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
        Assert.Contains(header, allowed.Split(',', StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_NativeOrigin_DoesNotAllowTheCsrfHeader()
    {
        // A app manda o csrf_token no corpo; o cabeçalho só serve a sessão por cookie da web.
        var response = await PreflightAsync("capacitor://localhost", WebLoginRoute, "POST", "X-CSRF-Token");

        // O ASP.NET devolve a lista da política; o browser recusa porque o cabeçalho não está nela.
        var allowed = response.Headers.TryGetValues("Access-Control-Allow-Headers", out var values)
            ? values.Single().Split(',', StringSplitOptions.TrimEntries)
            : [];
        Assert.DoesNotContain("X-CSRF-Token", allowed, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("capacitor://evil.test")]
    [InlineData("http://localhost")]
    [InlineData("https://localhost:8443")]
    [InlineData("ionic://localhost")]
    public async Task Preflight_LookAlikeOrigin_IsRejected(string origin)
    {
        var response = await PreflightAsync(origin, NativeLoginRoute, "POST", "Content-Type");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_WebOrigin_KeepsCredentials()
    {
        var response = await PreflightAsync(ApiWebApplicationFactory.AllowedOrigin, WebLoginRoute, "POST", "X-CSRF-Token");

        Assert.Equal(ApiWebApplicationFactory.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task NativeLogin_FromTheAppOrigin_GetsCorsHeadersWithoutCredentials()
    {
        var request = JsonPost(NativeLoginRoute, "capacitor://localhost");

        var response = await CreateClient().SendAsync(request, Token);

        // Credenciais erradas: o que importa aqui é que a WebView consegue ler a resposta.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("capacitor://localhost", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("capacitor://localhost")]
    [InlineData("https://localhost")]
    public async Task WebCookieLogin_FromTheAppOrigin_IsStill403(string origin)
    {
        var response = await CreateClient().SendAsync(JsonPost(WebLoginRoute, origin), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private static HttpRequestMessage JsonPost(string route, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent(
                """{"email":"nobody@example.test","password":"Wrong-Password-1!"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("Origin", origin);
        return request;
    }

    private Task<HttpResponseMessage> PreflightAsync(string origin, string route, string method, string headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, route);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return CreateClient().SendAsync(request, Token);
    }

    private HttpClient CreateClient() =>
        _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
}
