using Application;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Configuration;

public sealed class ApplicationRegistrationTests
{
    [Fact]
    public void AddApplication_RegistersEveryCurrentHandlerAndValidator()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var handlerCount = services.Count(descriptor =>
            descriptor.ServiceType.Name.EndsWith("Handler", StringComparison.Ordinal));
        var validatorCount = services.Count(descriptor =>
            descriptor.ServiceType.IsGenericType
            && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>));

        // 138 anteriores + 8 handlers 6A (portal, revisão de check-in e tomas).
        Assert.Equal(146, handlerCount);
        // 76 anteriores + 3 validators 6A (revisão e tomas não têm validator).
        Assert.Equal(79, validatorCount);
    }

    [Fact]
    public void AddApplication_NeverRegistersRequestScopedWorkAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.All(services, descriptor =>
            Assert.NotEqual(ServiceLifetime.Singleton, descriptor.Lifetime));
    }

    [Fact]
    public void AddApplication_RegistersEachServiceTypeExactlyOnce()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var duplicated = services
            .GroupBy(descriptor => descriptor.ServiceType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.FullName)
            .ToArray();

        Assert.Empty(duplicated);
    }

    [Fact]
    public void AddApplication_RegistersOnlyApplicationTypes()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.All(services, descriptor =>
            Assert.Equal(
                typeof(Application.DependencyInjection).Assembly,
                (descriptor.ImplementationType ?? descriptor.ServiceType).Assembly));
    }
}
