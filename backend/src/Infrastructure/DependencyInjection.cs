using Application.Common.Abstractions;
using Application.Features.Clients.Abstractions;
using Application.Features.Jobs.Abstractions;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Errors;
using Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

/// <summary>
/// Regista a camada de Infrastructure no container de DI
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured."
            );

        // Regista o DbContext com a connection string
        services.AddDbContext<PtManagerDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PtManagerDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            });

            options.AddInterceptors(sp.GetRequiredService<TenantWriteValidationInterceptor>());
        });

        // Scoped: o tenant é do pedido. Registado pelas duas interfaces para que o
        // middleware possa chamar Establish e o resto do código possa ler.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider =>
            provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(provider =>
            provider.GetRequiredService<TenantContext>());

        // Clients
        services.AddScoped<IClientStore, ClientStore>();
        services.AddScoped<IClientQueries, ClientQueries>();

        services.AddScoped<TenantWriteValidationInterceptor>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<PostgresConstraintTranslator>();

        // Repositórios
        services.AddScoped<IDurableJobStore, DurableJobRepository>();
        services.AddScoped<IOutboxStore, OutboxRepository>();

        return services;
    }
}
