using Application.Features.Billing.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence.Billing;

/// <summary>Compõe apenas os adapters provider-neutral de Billing.</summary>
internal static class BillingServiceCollectionExtensions
{
    internal static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IBillingCheckoutStore, BillingCheckoutStore>();
        services.AddScoped<ISubscriptionQueryStore, SubscriptionQueryStore>();
        services.AddScoped<IPaymentEventStore, PaymentEventStore>();

        return services;
    }
}
