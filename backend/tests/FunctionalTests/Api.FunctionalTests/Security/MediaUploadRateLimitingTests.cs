using System.Net;
using System.Security.Claims;
using Api.Authorization;
using Api.Configuration;
using Api.FunctionalTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Security;

public sealed class MediaUploadRateLimitingTests
{
    [Fact]
    public async Task UploadPolicy_ChangingIpDoesNotResetAuthenticatedUserBudget()
    {
        using var host = await MiddlewarePipelineHost.StartAsync(
            app =>
            {
                // Só este host de teste aceita identidade e IP por header.
                app.Use((context, next) =>
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ApiClaimNames.Subject, context.Request.Headers["X-Test-User"]!)], "test"));
                    context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Ip"]!);
                    return next();
                });
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints => endpoints.MapPut("/upload", () => Results.Ok())
                    .RequireRateLimiting(ApiRateLimitPolicyNames.MediaUpload));
            },
            services =>
            {
                services.AddRouting();
                services.AddApiRateLimiting();
            });
        using var client = host.GetTestClient();
        var attempts = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await SendAsync(client, "owner", "10.0.0.1");
            attempts.Add(response.StatusCode);
        }

        using var rejected = await SendAsync(client, "owner", "10.0.0.2");
        using var otherUser = await SendAsync(client, "neighbour", "10.0.0.2");

        Assert.All(attempts, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string user, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/upload");
        request.Headers.Add("X-Test-User", user);
        request.Headers.Add("X-Test-Ip", ip);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
