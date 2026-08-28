using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Arranca um pipeline HTTP mínimo em memória para provar middlewares isolados,
/// com a mesma semântica de <c>OnStarting</c> do servidor real.
/// </summary>
internal static class MiddlewarePipelineHost
{
    public static async Task<IHost> StartAsync(
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddProblemDetails();
                    configureServices?.Invoke(services);
                })
                .Configure(configure))
            .StartAsync();

        return host;
    }
}
