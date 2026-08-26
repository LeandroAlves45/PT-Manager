using System.Reflection;
using Application.Features.Authentication.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

public sealed class AuthArchitectureTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(Application.Features.Authentication.AuthenticationPolicy).Assembly;
    private static readonly Assembly InfrastructureAssembly =
        typeof(Infrastructure.Data.PtManagerDbContext).Assembly;

    [Theory]
    [InlineData("Microsoft.AspNetCore.Identity")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    public void ApplicationAuthentication_DoesNotReferenceInfrastructureFrameworks(
        string assemblyName)
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == assemblyName);
    }

    [Theory]
    [InlineData(
        "Infrastructure.Identity.EmailConfirmationStore",
        "Application.Features.Authentication.Abstractions.IEmailConfirmationStore")]
    [InlineData(
        "Infrastructure.Identity.PasswordResetRequestStore",
        "Application.Features.Authentication.Abstractions.IPasswordResetRequestStore")]
    [InlineData(
        "Infrastructure.Identity.ClientInvitationStore",
        "Application.Features.Authentication.Abstractions.IClientInvitationStore")]
    public void AuthenticationLinks_UseSeparateAdapters(
        string implementationName,
        string contractName)
    {
        var implementation = InfrastructureAssembly.GetType(implementationName);

        Assert.NotNull(implementation);
        Assert.Contains(
            implementation!.GetInterfaces(),
            contract => contract.FullName == contractName);
        Assert.Null(InfrastructureAssembly.GetType("Infrastructure.Identity.AuthLinkStore"));
    }

    [Fact]
    public void InfrastructureComposition_RegistersAuthenticationStores()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] =
            "Host=localhost;Database=pt_manager_tests";
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        Assert.Contains(
            services,
            service => service.ServiceType == typeof(IAuthenticationRegistrationStore));
        Assert.Contains(
            services,
            service => service.ServiceType == typeof(IAuthenticationSessionStore));
        Assert.Contains(
            services,
            service => service.ServiceType == typeof(IPasswordManagementStore));
    }
}
