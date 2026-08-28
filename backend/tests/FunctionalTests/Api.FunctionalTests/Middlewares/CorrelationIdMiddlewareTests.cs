using Api.FunctionalTests.Support;
using Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Api.FunctionalTests.Middlewares;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_KeepsSuppliedSafeCorrelationId()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "req-123.abc_XYZ");

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(
            "req-123.abc_XYZ",
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Theory]
    [InlineData("value with spaces")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("valor\"injetado")]
    public async Task InvokeAsync_ReplacesHostileCorrelationId(string hostile)
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName,
            hostile);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        var propagated = response.Headers
            .GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.NotEqual(hostile, propagated);
        Assert.True(Guid.TryParseExact(propagated, "N", out _));
    }

    [Fact]
    public async Task InvokeAsync_ReplacesCorrelationIdLongerThanTheAllowedLength()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();
        var oversized = new string('a', 65);
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, oversized);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.NotEqual(
            oversized,
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationIdWhenHeaderIsAbsent()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(Guid.TryParseExact(
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single(),
            "N",
            out _));
    }

    [Fact]
    public async Task InvokeAsync_ExposesTheCorrelationIdAsTraceIdentifier()
    {
        using var host = await StartHostAsync(context =>
            context.Response.WriteAsync(context.TraceIdentifier));
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "trace-1");

        var body = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal("trace-1", body);
    }

    private static Task<Microsoft.Extensions.Hosting.IHost> StartHostAsync(
        RequestDelegate? terminal = null) =>
        MiddlewarePipelineHost.StartAsync(app =>
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.Run(terminal ?? (_ => Task.CompletedTask));
        });
}
