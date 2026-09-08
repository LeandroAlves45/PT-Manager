using System.Net.Http.Headers;
using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;
using Infrastructure.Identity;
using Infrastructure.Jobs.QStash;
using Infrastructure.Notifications;
using Infrastructure.Persistence.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs;

/// <summary>Compõe os adapters da execução durável.</summary>
public static class JobDispatchInfrastructureExtensions
{
    public static IServiceCollection AddJobDispatchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JobDispatchOptions>()
            .Bind(configuration.GetSection(JobDispatchOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Configuration section 'JobDispatch' is missing or invalid.")
            .ValidateOnStart();

        services.AddOptions<QStashOptions>()
            .Bind(configuration.GetSection(QStashOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Configuration section 'QStash' is invalid.")
            .ValidateOnStart();

        services.AddScoped<JobTenantValidator>();
        services.AddScoped<QStashReplayStore>();
        services.AddScoped<IInternalDispatchRequestAuthenticator, QStashRequestAuthenticator>();
        services.AddScoped<INotificationDeliveryStore, NotificationDeliveryStore>();

        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<OutboxDispatcher>();
        services.AddSingleton<IJobDispatchActivation, JobDispatchActivation>();

        services.AddHttpClient<INotificationDeliveryGateway, ResendNotificationDeliveryGateway>(
            (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ResendOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.ApiKey);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

        return services;
    }
}
