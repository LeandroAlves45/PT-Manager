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

        // 156 anteriores + 8 handlers 6GB-SU (procura, isentos, isenção, convidar, convites,
        // anular, estado e registo do convite de treinador).
        // + 2 do trainer (estado do convite na ficha, conclusões de treino).
        // + 4 do portal do cliente (dias passados, dia passado, lista e escolha do plano alimentar).
        // + 4 da avaliação inicial (estado e gravação no portal, revista, lista por rever).
        // + 3 das tomas dos suplementos do plano (ler, marcar, desmarcar).
        // + 4 da moderação (bloquear e desbloquear suplemento, bloquear e levantar personal trainer).
        // + 1 do portal do cliente (eliminar a conta).
        // + 2 da importação CSV do catálogo global (alimentos e exercícios).
        Assert.Equal(184, handlerCount);
        // 82 anteriores + 5 validators 6GB-SU (procura, isentos, convidar, estado e registo).
        // + 1 do trainer (conclusões de treino).
        // + 2 da avaliação inicial (gravação no portal, lista por rever).
        // + 2 da moderação (bloquear suplemento, bloquear personal trainer).
        // + 1 do portal do cliente (eliminar a conta).
        Assert.Equal(93, validatorCount);
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
