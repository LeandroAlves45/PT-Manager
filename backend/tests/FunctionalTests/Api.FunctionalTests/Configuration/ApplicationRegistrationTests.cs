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

        // 124 + 4 handlers de moderação administrativa (Block/Unblock de Food e Exercise).
        Assert.Equal(128, handlerCount);
        // 72 + 2 validators de moderação (BlockFood e BlockExercise; Unblock não tem payload).
        Assert.Equal(74, validatorCount);
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
