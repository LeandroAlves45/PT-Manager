using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Api.Authorization;
using Api.Configuration;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.FunctionalTests.Security;

/// <summary>
/// Prova o limitador global: 300 pedidos por minuto por utilizador autenticado e
/// 60 por minuto por IP anónimo.
/// </summary>
/// <remarks>
/// Os testes correm sobre um pipeline mínimo com <c>AddApiRateLimiting</c> real, e não
/// sobre o host completo. É deliberado: esgotar a janela exige centenas de pedidos, e
/// fazê-los atravessar autenticação, tenant e base de dados provaria o custo do
/// pipeline em vez da regra do limitador. A configuração em prova é a de produção.
/// </remarks>
public sealed class ApiGlobalRateLimitingTests
{
    private const string TestIpHeader = "X-Test-Client-Ip";
    private const string TestSubjectHeader = "X-Test-Subject";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AuthenticatedUser_IsAllowedThreeHundredRequestsPerMinute()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        var subject = Guid.NewGuid().ToString();

        var statuses = await SendAsync(client, "10.1.0.1", 300, subject);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task AuthenticatedUser_IsRejectedOnTheRequestAfterTheLimit()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        var subject = Guid.NewGuid().ToString();

        var statuses = await SendAsync(client, "10.1.0.2", 301, subject);

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task AuthenticatedPartitions_AreIsolatedByUserAndNotByIp()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        const string sharedIp = "10.1.0.3";
        await SendAsync(client, sharedIp, 301, Guid.NewGuid().ToString());

        // Mesmo IP, utilizador diferente: a janela do primeiro não pode afetar o segundo.
        var otherUser = await SendAsync(client, sharedIp, 1, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, otherUser[0]);
    }

    [Fact]
    public async Task AnonymousCaller_IsAllowedSixtyRequestsPerMinute()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var statuses = await SendAsync(client, "10.2.0.1", 60, subject: null);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task AnonymousCaller_IsRejectedOnTheRequestAfterTheLimit()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var statuses = await SendAsync(client, "10.2.0.2", 61, subject: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task AnonymousPartitions_AreIsolatedByIp()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        await SendAsync(client, "10.2.0.3", 61, subject: null);

        var otherIp = await SendAsync(client, "10.2.0.4", 1, subject: null);

        Assert.Equal(HttpStatusCode.OK, otherIp[0]);
    }

    [Fact]
    public async Task GlobalRejection_ReturnsProblemDetailsWithRetryAfter()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        await SendAsync(client, "10.2.0.5", 60, subject: null);

        var response = await SendRequestAsync(client, "10.2.0.5", subject: null);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Headers.RetryAfter);

        var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(
            "Too many requests",
            payload.RootElement.GetProperty("title").GetString());
        Assert.True(payload.RootElement.TryGetProperty("correlation_id", out _));
    }

    private static async Task<HttpStatusCode[]> SendAsync(
        HttpClient client,
        string ip,
        int requests,
        string? subject)
    {
        var statuses = new HttpStatusCode[requests];
        for (var attempt = 0; attempt < requests; attempt++)
            statuses[attempt] = (await SendRequestAsync(client, ip, subject)).StatusCode;

        return statuses;
    }

    private static Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        string ip,
        string? subject)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(TestIpHeader, ip);
        if (subject is not null)
            request.Headers.Add(TestSubjectHeader, subject);

        return client.SendAsync(request, Token);
    }

    private static Task<IHost> StartHostAsync() =>
        MiddlewarePipelineHost.StartAsync(
            app =>
            {
                app.Use((context, next) =>
                {
                    context.Connection.RemoteIpAddress =
                        System.Net.IPAddress.Parse(context.Request.Headers[TestIpHeader]!);

                    // O limitador global decide pela identidade autenticada e pela claim
                    // "sub". Estabelecê-la aqui reproduz o estado que o handler bearer
                    // deixa no contexto, sem exigir um token real por pedido.
                    var subject = context.Request.Headers[TestSubjectHeader].ToString();
                    if (!string.IsNullOrEmpty(subject))
                    {
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ApiClaimNames.Subject, subject)],
                            authenticationType: "Test"));
                    }

                    return next();
                });

                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints => endpoints.MapGet("/probe", () => Results.Ok()));
            },
            services =>
            {
                services.AddRouting();
                services.AddApiRateLimiting();
            });
}
