using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Health;

/// <summary>
/// Confirma que o PostgreSql aceita ligações e que o schema corresponde ás migrations da aplicação.
/// </summary>
public sealed class DatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider
                .GetRequiredService<PtManagerDbContext>()
                .Database;

            if (!await database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");

            var pendingMigrations = (await database
                .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();

            return pendingMigrations.Length == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"Pending migrations: {string.Join(", ", pendingMigrations)}");
        }
        catch (Exception exception)
        {
            // Um schema ausente é indisponibilidade. O health check nunca tenta repará-lo.
            return HealthCheckResult.Unhealthy(
                "Database readiness check failed.",
                exception);
        }
    }
}
