using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Notifications;
using Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence.Billing;

/// <summary>Compõe apenas os adapters provider-neutral de Billing.</summary>
internal static class BillingServiceCollectionExtensions
{
    internal static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<IBillingCheckoutStore, BillingCheckoutStore>();
        services.AddScoped<ISubscriptionQueryStore, SubscriptionQueryStore>();
        services.AddScoped<IPaymentEventStore, PaymentEventStore>();
        services.AddScoped<IBillingNotificationRecipientStore, BillingNotificationRecipientStore>();
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<StripeOptions>, StripeOptionsValidator>();
        services.AddSingleton<StripeClientFactory>();
        services.AddScoped<ICheckoutGateway, StripeCheckoutGateway>();
        services.AddScoped<ICustomerPortalGateway, StripeCustomerPortalGateway>();
        services.AddScoped<ISubscriptionReconciliationGateway, StripeSubscriptionReconciliationGateway>();
        services.AddScoped<IPaymentWebhookAuthenticator, StripePaymentWebhookAuthenticator>();

        return services;
    }
}
