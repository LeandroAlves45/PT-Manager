using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Api.Authorization;
using Api.FunctionalTests.Support;
using Application.Features.Authentication.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Security;

/// <summary>
/// Prova o pipeline JWT real: emissor, validação bearer e estabelecimento de tenant.
/// </summary>
public sealed class JwtAuthenticationTests : IDisposable
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    private readonly ApiWebApplicationFactory _factory =
        new(UnusedConnectionString, "Testing");

    [Fact]
    public async Task TrainerToken_ReachesTrainerOnlyEndpointWithoutUnauthorized()
    {
        var trainerId = Guid.NewGuid();
        var token = TestJwtFactory.IssueTrainer(trainerId);
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientToken_ReachesClientOnlyEndpointWithoutUnauthorized()
    {
        var clientUserId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var token = TestJwtFactory.IssueClient(clientUserId, trainerId);
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostAcceptInviteAsync(client);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientToken_OnTrainerEndpoint_ReturnsForbiddenNotUnauthorized()
    {
        var clientUserId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var token = TestJwtFactory.IssueClient(clientUserId, trainerId);
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperuserToken_ReachesAdministrativeEndpointWithoutUnauthorized()
    {
        var token = TestJwtFactory.IssueSuperuser(Guid.NewGuid());
        var client = CreateHttpsClient().WithBearer(token);

        var response = await PostAdminBlockFoodAsync(client);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TrainerToken_WithMismatchedTenantClaim_ReturnsUnauthorized()
    {
        var trainerId = Guid.NewGuid();
        var token = TestJwtFactory.Issue(trainerId, ApiRoleNames.Trainer, Guid.NewGuid());
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorizedWithBearerChallenge()
    {
        var trainerId = Guid.NewGuid();
        var token = TestJwtFactory.Issue(
            trainerId,
            ApiRoleNames.Trainer,
            trainerId,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertBearerChallenge(response);
    }

    [Fact]
    public async Task TokenSignedWithDifferentKey_ReturnsUnauthorized()
    {
        var trainerId = Guid.NewGuid();
        var foreignKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var token = TestJwtFactory.IssueWithSigningKey(
            trainerId,
            ApiRoleNames.Trainer,
            trainerId,
            foreignKey);
        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertBearerChallenge(response);
    }

    [Fact]
    public async Task ProductionIssuerToken_IsAcceptedByTheHttpPipeline()
    {
        var trainerId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        var token = issuer.Issue(new AuthenticatedPrincipal(
            trainerId,
            trainerId,
            ApiRoleNames.Trainer,
            "functional-test-stamp")).Token;

        var client = _factory.CreateOriginClient().WithBearer(token);

        var response = await PostInviteClientAsync(client);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateHttpsClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static Task<HttpResponseMessage> PostInviteClientAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/invite-client",
            new { client_id = Guid.Empty, email = string.Empty },
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> PostAcceptInviteAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/accept-invite",
            new { token = string.Empty, transfer_approved = false },
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> PostAdminBlockFoodAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            $"/api/v1/admin/content-moderation/foods/{Guid.NewGuid()}/block",
            new { reason_code = "malicious_content" },
            TestContext.Current.CancellationToken);

    private static void AssertBearerChallenge(HttpResponseMessage response)
    {
        Assert.True(response.Headers.Contains("WWW-Authenticate"));
        Assert.Contains(
            "Bearer",
            response.Headers.GetValues("WWW-Authenticate").Single(),
            StringComparison.OrdinalIgnoreCase);
    }
}
