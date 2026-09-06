using System.Reflection;
using Application.Features.Authentication.Google.Abstractions;
using Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

/// <summary>
/// Garante que a dependência Google fica confinada a Infrastructure e que a composição
/// expõe apenas portas provider-neutral.
/// </summary>
public sealed class GoogleAuthenticationArchitectureTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(IExternalIdentityVerifier).Assembly;

    [Fact]
    public void Application_DoesNotReferenceGoogleApisAuth()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Google.Apis.Auth");
    }

    [Fact]
    public void Application_ExposesNoGoogleSpecificPortTypes()
    {
        // As portas são nomeadas por capacidade, não por fornecedor: trocar de provider
        // não pode obrigar a mexer na Application.
        var portTypes = new[]
        {
            typeof(IExternalIdentityVerifier),
            typeof(IExternalChallengeStore),
            typeof(IExternalAuthenticationStore)
        };

        Assert.All(portTypes, type =>
            Assert.DoesNotContain("Google", type.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_RegistersProviderNeutralPorts()
    {
        var configuration = new ConfigurationManager();
        configuration["Google:ClientId"] = "client.apps.googleusercontent.com";
        var services = new ServiceCollection();

        services.AddGoogleAuthenticationInfrastructure(configuration);

        Assert.Contains(services,
            service => service.ServiceType == typeof(IExternalIdentityVerifier));
        Assert.Contains(services,
            service => service.ServiceType == typeof(IExternalAuthenticationStore));
        Assert.Contains(services,
            service => service.ServiceType == typeof(IExternalChallengeStore));
    }
}
