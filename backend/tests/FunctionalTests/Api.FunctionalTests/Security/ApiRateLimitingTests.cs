using System.Net;
using System.Text.Json;
using Api.Configuration;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.FunctionalTests.Security;

public sealed class ApiRateLimitingTests
{
    private const string TestIpHeader = "X-Test-Client-Ip";

    [Fact]
    public async Task LoginPolicy_AllowsTheConfiguredNumberOfAttempts()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var statuses = await SendAsync(client, "10.0.0.1", 10);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task LoginPolicy_RejectsTheAttemptAfterTheLimit()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var statuses = await SendAsync(client, "10.0.0.2", 11);

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task LoginPolicy_IsolatesPartitionsByClientIp()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        await SendAsync(client, "10.0.0.3", 11);

        var otherClient = await SendAsync(client, "10.0.0.4", 1);

        Assert.Equal(HttpStatusCode.OK, otherClient[0]);
    }

    [Fact]
    public async Task Rejection_ReturnsProblemDetailsWithRetryAfter()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        for (var attempt = 0; attempt < 10; attempt++)
            await SendAsync(client, "10.0.0.5", 1);

        var response = await SendRequestAsync(client, "10.0.0.5");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Headers.RetryAfter);
        var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Too many requests", payload.RootElement.GetProperty("title").GetString());
    }

    private static async Task<HttpStatusCode[]> SendAsync(
        HttpClient client,
        string ip,
        int requests)
    {
        var statuses = new HttpStatusCode[requests];
        for (var attempt = 0; attempt < requests; attempt++)
            statuses[attempt] = (await SendRequestAsync(client, ip)).StatusCode;

        return statuses;
    }

    private static Task<HttpResponseMessage> SendRequestAsync(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/login");
        request.Headers.Add(TestIpHeader, ip);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<IHost> StartHostAsync() =>
        MiddlewarePipelineHost.StartAsync(
            app =>
            {
                // O IP de teste substitui a ligação real para provar o particionamento.
                app.Use((context, next) =>
                {
                    context.Connection.RemoteIpAddress =
                        System.Net.IPAddress.Parse(context.Request.Headers[TestIpHeader]!);
                    return next();
                });

                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                    endpoints.MapPost("/login", () => Results.Ok())
                        .RequireRateLimiting(ApiRateLimitPolicyNames.Login));
            },
            services =>
            {
                services.AddRouting();
                services.AddApiRateLimiting();
            });
}
