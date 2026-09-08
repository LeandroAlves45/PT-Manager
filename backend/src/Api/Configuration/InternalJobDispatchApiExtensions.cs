using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Configuration;

/// <summary>Compõe a fronteira HTTP da ativação interna.</summary>
public static class InternalJobDispatchApiExtensions
{
    public static IServiceCollection AddInternalJobDispatchApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<InternalJobDispatchHttpOptions>()
            .Bind(configuration.GetSection(InternalJobDispatchHttpOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Configuration section 'QStash' has an invalid HTTP body limit.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
            options.AddPolicy(
                InternalJobDispatchPolicyNames.Dispatch,
                context => RateLimitPartition.GetFixedWindowLimiter(
                    $"qstash:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })));

        return services;
    }
}
