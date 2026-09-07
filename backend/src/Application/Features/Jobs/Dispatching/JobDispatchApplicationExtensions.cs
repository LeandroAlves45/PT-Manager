using Application.Features.Notifications.Delivery;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Jobs.Dispatching;

/// <summary>Regista os casos de uso de execução durável da Application.</summary>
public static class JobDispatchApplicationExtensions
{
    public static IServiceCollection AddJobDispatchApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDurableJobHandler, SendNotificationJobHandler>();
        return services;
    }
}
