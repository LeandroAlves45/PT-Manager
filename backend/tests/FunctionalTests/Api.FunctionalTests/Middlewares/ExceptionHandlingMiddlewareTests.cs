using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Api.FunctionalTests.Middlewares;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_TranslatesInvalidPrincipalIntoUnauthorizedProblem()
    {
        using var host = await StartHostAsync(() => throw new InvalidAuthenticatedPrincipalException());

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("The authenticated identity is invalid.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_TranslatesUnexpectedFailureIntoGenericProblem()
    {
        using var host = await StartHostAsync(() => throw new InvalidOperationException(
            "connection string password=supersecret"));

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_NeverLeaksExceptionDetailToTheClient()
    {
        using var host = await StartHostAsync(() => throw new InvalidOperationException(
            "connection string password=supersecret"));

        var response = await host.GetTestClient()
            .GetAsync("/", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("supersecret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at Api.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_KeepsTheCorrelationIdInTheProblemPayload()
    {
        using var host = await MiddlewarePipelineHost.StartAsync(app =>
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.Run(_ => throw new InvalidOperationException("boom"));
        });
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "corr-42");

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        var problem = await ReadProblemAsync(response);
        Assert.Equal("corr-42", problem.GetProperty("correlation_id").GetString());
    }

    [Fact]
    public async Task InvokeAsync_LeavesSuccessfulResponsesUntouched()
    {
        using var host = await MiddlewarePipelineHost.StartAsync(app =>
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.Run(context => context.Response.WriteAsync("ok"));
        });

        var body = await host.GetTestClient()
            .GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal("ok", body);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(payload).RootElement;
    }

    private static Task<Microsoft.Extensions.Hosting.IHost> StartHostAsync(Action throwing) =>
        MiddlewarePipelineHost.StartAsync(app =>
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.Run(_ =>
            {
                throwing();
                return Task.CompletedTask;
            });
        });
}
