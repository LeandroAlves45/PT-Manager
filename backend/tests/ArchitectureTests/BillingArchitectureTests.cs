using System.Reflection;
using Application.Features.Billing.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

public sealed class BillingArchitectureTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(Application.Features.Billing.BillingErrors).Assembly;

    [Fact]
    public void BillingApplication_HasCapabilitySpecificPortsWithoutLegacyMonolithicPorts()
    {
        var typeNames = ApplicationAssembly.GetTypes()
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(
            "Application.Features.Billing.Abstractions.ICheckoutGateway",
            typeNames);
        Assert.Contains(
            "Application.Features.Billing.Abstractions.ICustomerPortalGateway",
            typeNames);
        Assert.Contains(
            "Application.Features.Billing.Abstractions.ISubscriptionReconciliationGateway",
            typeNames);
        Assert.Contains(
            "Application.Features.Billing.Abstractions.ISubscriptionQueryStore",
            typeNames);
        Assert.Contains(
            "Application.Features.Billing.Abstractions.IBillingCheckoutStore",
            typeNames);
        Assert.Contains(
            "Application.Features.Billing.Abstractions.IPaymentEventStore",
            typeNames);
        Assert.DoesNotContain(
            "Application.Features.Billing.Abstractions.IPaymentGateway",
            typeNames);
        Assert.DoesNotContain(
            "Application.Features.Billing.Abstractions.IBillingStore",
            typeNames);
    }

    [Theory]
    [InlineData("Stripe.net")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    public void BillingApplication_DoesNotReferenceProviderOrPersistence(
        string dependencyName)
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == dependencyName);
    }

    [Fact]
    public void InfrastructureComposition_RegistersBillingStoresWithConcreteAdapters()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] =
            "Host=localhost;Database=pt_manager_tests";
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        AssertStoreRegistration(
            services,
            typeof(IBillingCheckoutStore),
            "Infrastructure.Persistence.Billing.BillingCheckoutStore");
        AssertStoreRegistration(
            services,
            typeof(ISubscriptionQueryStore),
            "Infrastructure.Persistence.Billing.SubscriptionQueryStore");
        AssertStoreRegistration(
            services,
            typeof(IPaymentEventStore),
            "Infrastructure.Persistence.Billing.PaymentEventStore");
    }

    private static void AssertStoreRegistration(
        IServiceCollection services,
        Type contract,
        string implementationName)
    {
        var registration = Assert.Single(
            services,
            service => service.ServiceType == contract);

        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.Equal(implementationName, registration.ImplementationType?.FullName);
    }
}
