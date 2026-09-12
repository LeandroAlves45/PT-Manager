using System.Reflection;
using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;

namespace ArchitectureTests;

/// <summary>
/// Verifica o isolamento de camadas da execução durável.
/// A Application define contratos de dispatch e entrega; QStash, EF Core e
/// Resend só podem existir em Infrastructure ou Api. Se um destes limites
/// cedesse, o handler deixaria de ser testável sem infraestrutura real.
/// </summary>
public sealed class JobDispatchArchitectureTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(DispatchItemOutcome).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(Infrastructure.Data.PtManagerDbContext).Assembly;

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.IdentityModel.JsonWebTokens")]
    public void ApplicationDispatch_DoesNotReferenceInfrastructureFrameworks(
        string assemblyName)
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == assemblyName);
    }

    [Fact]
    public void QStashTypes_LiveOnlyInInfrastructure()
    {
        // Nenhum tipo com nome QStash pode existir na Application: o transporte
        // de activação é um detalhe substituível.
        Assert.DoesNotContain(
            ApplicationAssembly.GetTypes(),
            type => type.FullName!.Contains("QStash", StringComparison.Ordinal));

        Assert.Contains(
            InfrastructureAssembly.GetTypes(),
            type => type.FullName == "Infrastructure.Jobs.QStash.QStashRequestAuthenticator");
    }

    [Fact]
    public void ResendTypes_LiveOnlyInInfrastructure()
    {
        // "Resend" aparece legitimamente em casos de uso como
        // ResendEmailConfirmation. O que não pode existir na Application é o
        // adapter do fornecedor: transporte, gateway ou opções.
        Assert.DoesNotContain(
            ApplicationAssembly.GetTypes(),
            type => type.Name.StartsWith("Resend", StringComparison.Ordinal) &&
                (type.Name.Contains("Transport", StringComparison.Ordinal) ||
                    type.Name.Contains("Gateway", StringComparison.Ordinal) ||
                    type.Name.Contains("Options", StringComparison.Ordinal) ||
                    type.Name.Contains("Sender", StringComparison.Ordinal)));

        Assert.Contains(
            InfrastructureAssembly.GetTypes(),
            type => type.FullName == "Infrastructure.Email.ResendEmailTransport");
    }

    [Fact]
    public void DispatchPorts_AreDefinedInApplication()
    {
        Assert.Equal(ApplicationAssembly, typeof(IDurableJobHandler).Assembly);
        Assert.Equal(ApplicationAssembly, typeof(IOutboxMessageHandler).Assembly);
        Assert.Equal(ApplicationAssembly, typeof(IJobDispatchActivation).Assembly);
        Assert.Equal(
            ApplicationAssembly,
            typeof(IInternalDispatchRequestAuthenticator).Assembly);
        Assert.Equal(ApplicationAssembly, typeof(INotificationDeliveryGateway).Assembly);
        Assert.Equal(ApplicationAssembly, typeof(INotificationDeliveryStore).Assembly);
    }

    [Theory]
    [InlineData("Infrastructure.Jobs.JobDispatchActivation", typeof(IJobDispatchActivation))]
    [InlineData(
        "Infrastructure.Jobs.QStash.QStashRequestAuthenticator",
        typeof(IInternalDispatchRequestAuthenticator))]
    [InlineData(
        "Infrastructure.Persistence.Notifications.NotificationDeliveryStore",
        typeof(INotificationDeliveryStore))]
    [InlineData(
        "Infrastructure.Notifications.ResendNotificationDeliveryGateway",
        typeof(INotificationDeliveryGateway))]
    public void InfrastructureAdapters_ImplementTheApplicationPort(
        string implementationName,
        Type contract)
    {
        var implementation = InfrastructureAssembly.GetType(implementationName);

        Assert.NotNull(implementation);
        Assert.Contains(implementation!.GetInterfaces(), type => type == contract);
    }

    [Fact]
    public void SendNotificationHandler_IsTheOnlyDurableJobHandlerInApplication()
    {
        var handlers = ApplicationAssembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                typeof(IDurableJobHandler).IsAssignableFrom(type))
            .ToArray();

        // A allowlist da Fase 5A tem exactamente um job real.
        var handler = Assert.Single(handlers);
        Assert.Equal(typeof(SendNotificationJobHandler), handler);
    }

    [Fact]
    public void OutboxHandlers_LiveOnlyInInfrastructure()
    {
        // Um handler de outbox na Application arrastaria fornecedores externos
        // para a camada de casos de uso. Todos os consumidores reais vivem em
        // Infrastructure, ao lado dos adapters que chamam.
        Assert.DoesNotContain(
            ApplicationAssembly.GetTypes(),
            type => type is { IsClass: true, IsAbstract: false } &&
                typeof(IOutboxMessageHandler).IsAssignableFrom(type));
    }

    [Fact]
    public void OutboxHandlers_MatchTheClosedAllowlist()
    {
        // Allowlist positiva: acrescentar um consumidor de outbox é uma decisão
        // que tem de passar por revisão, porque o OutboxDispatcher passa a
        // reclamar mensagens desse tipo no instante em que ele é registado.
        var handlers = InfrastructureAssembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                typeof(IOutboxMessageHandler).IsAssignableFrom(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Infrastructure.Jobs.BillingNotificationOutboxHandler",
                "Infrastructure.Jobs.ClientAvatarDeletionOutboxHandler",
                "Infrastructure.Jobs.TrainerLogoDeletionOutboxHandler"
            ],
            handlers);
    }

    [Fact]
    public void InternalJobsController_DoesNotDependOnEntityFramework()
    {
        var controller = typeof(Program).Assembly
            .GetType("Api.Controllers.InternalJobsController");

        Assert.NotNull(controller);

        var constructorParameterTypes = controller!
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            constructorParameterTypes,
            name => name.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
                name.Contains("PtManagerDbContext", StringComparison.Ordinal));
    }
}
