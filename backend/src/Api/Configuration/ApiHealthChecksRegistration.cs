using Api.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Api.Configuration;

/// <summary>Regista e mapeia os health checks públicos da API.</summary>
public static class ApiHealthChecksRegistration
{
    /// <summary>Etiqueta aplicada ás verificações necessárias para aceitar tráfego.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessHealthCheck>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag]);

        return services;
    }

    /// <summary>Mapeia vivacidade e readiness sem autenticação nem rate limiting.</summary>
    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Vivacidade não consulta dependências externas.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        })
        .AllowAnonymous()
        .DisableRateLimiting();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag)
        })
        .AllowAnonymous()
        .DisableRateLimiting();

        return endpoints;
    }
}
