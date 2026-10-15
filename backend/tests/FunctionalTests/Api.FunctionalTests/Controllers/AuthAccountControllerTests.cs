using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.FunctionalTests.Controllers;

/// <summary>Leitura dos métodos de entrada da conta autenticada.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthAccountControllerTests : IAsyncLifetime
{
    private const string AccountRoute = "/api/v1/auth/account";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthAccountControllerTests(PostgresApiFixture database) => _database = database;

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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Get_ReportsPasswordAndGoogleIndependently(bool hasPassword, bool googleLinked)
    {
        var email = $"account-{Guid.NewGuid():N}@trainer.test";
        var userId = await AuthTestData.SeedAccountAsync(
            _factory, email, withPassword: hasPassword, googleLinked: googleLinked);

        var response = await GetAsync(TestJwtFactory.IssueTrainer(userId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("trainer", body.RootElement.GetProperty("role").GetString());
        Assert.Equal(hasPassword, body.RootElement.GetProperty("has_password").GetBoolean());
        Assert.Equal(googleLinked, body.RootElement.GetProperty("google_linked").GetBoolean());
    }

    [Fact]
    public async Task Get_ClientAccount_ReportsTheClientRole()
    {
        var email = $"account-client-{Guid.NewGuid():N}@client.test";
        var userId = await AuthTestData.SeedAccountAsync(_factory, email, role: "client");

        var response = await GetAsync(TestJwtFactory.IssueClient(userId, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadAsync(response);
        Assert.Equal("client", body.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Get_ResponseIsNotCacheable()
    {
        var userId = await AuthTestData.SeedAccountAsync(
            _factory, $"account-cache-{Guid.NewGuid():N}@trainer.test");

        var response = await GetAsync(TestJwtFactory.IssueTrainer(userId));

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task Get_Anonymous_Is401()
    {
        var response = await _factory.CreateOriginClient().GetAsync(
            AccountRoute, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_InactiveAccountWithValidToken_Is401()
    {
        var userId = await AuthTestData.SeedAccountAsync(
            _factory, $"account-inactive-{Guid.NewGuid():N}@trainer.test", active: false);

        var response = await GetAsync(TestJwtFactory.IssueTrainer(userId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutOriginHeader_StillWorksBecauseTheBearerIsNotAmbient()
    {
        var userId = await AuthTestData.SeedAccountAsync(
            _factory, $"account-origin-{Guid.NewGuid():N}@trainer.test");
        // HTTPS e sem redirecionamentos: um redirect para HTTPS deixaria cair o Authorization.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        }).WithBearer(TestJwtFactory.IssueTrainer(userId));

        var response = await client.GetAsync(AccountRoute, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> GetAsync(string accessToken) =>
        _factory.CreateOriginClient().WithBearer(accessToken)
            .GetAsync(AccountRoute, TestContext.Current.CancellationToken);

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
