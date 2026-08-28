using Api.Configuration;
using Api.FunctionalTests.Support;
using Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Api.FunctionalTests.Middlewares;

public sealed class SecurityHeadersMiddlewareTests
{
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    public async Task InvokeAsync_WritesDefensiveHeaders(string header, string expected)
    {
        using var host = await StartHostAsync(sensitiveEndpoint: false);

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.Headers.GetValues(header).Single());
    }

    [Fact]
    public async Task InvokeAsync_DeniesEveryContentSourceAndFraming()
    {
        using var host = await StartHostAsync(sensitiveEndpoint: false);

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotDisableCacheForOrdinaryEndpoints()
    {
        using var host = await StartHostAsync(sensitiveEndpoint: false);

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Null(response.Headers.CacheControl);
    }

    [Fact]
    public async Task InvokeAsync_DisablesCacheOnSensitiveEndpoints()
    {
        using var host = await StartHostAsync(sensitiveEndpoint: true);

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("no-cache", response.Headers.GetValues("Pragma"));
    }

    private static Task<Microsoft.Extensions.Hosting.IHost> StartHostAsync(bool sensitiveEndpoint) =>
        MiddlewarePipelineHost.StartAsync(app =>
        {
            if (sensitiveEndpoint)
                app.Use((context, next) =>
                {
                    context.SetEndpoint(new Endpoint(
                        _ => Task.CompletedTask,
                        new EndpointMetadataCollection(new SensitiveResponseAttribute()),
                        "sensitive_test"));
                    return next();
                });

            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.Run(_ => Task.CompletedTask);
        });
}
