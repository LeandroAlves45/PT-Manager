using System.Reflection;
using Application.Common.Abstractions;
using Application.Features.ClientPortal.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

/// <summary>
/// Congela a fronteira das imagens geridas: SkiaSharp, Cloudinary e Google
/// Vision existem só em Infrastructure, e a Application vê apenas as três portas.
/// </summary>
public sealed class MediaArchitectureTests
{
    private static readonly Assembly ApplicationAssembly = typeof(IMediaStorage).Assembly;

    [Theory]
    [InlineData("SkiaSharp")]
    [InlineData("Google.Apis.Auth")]
    public void Application_DoesNotReferenceMediaProviders(string assemblyName)
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == assemblyName);
    }

    [Theory]
    [InlineData("Cloudinary")]
    [InlineData("Skia")]
    [InlineData("SafeSearch")]
    public void ProviderTypes_LiveOnlyInInfrastructure(string providerName)
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetTypes(),
            type => type.FullName!.Contains(providerName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(IMediaStorage))]
    [InlineData(typeof(IImageProcessor))]
    [InlineData(typeof(IImageModerationService))]
    public void MediaPorts_AreDeclaredInApplication(Type port)
    {
        Assert.Equal(ApplicationAssembly, port.Assembly);
        Assert.True(port.IsInterface);
    }

    [Theory]
    [InlineData("SkiaSharp")]
    [InlineData("SkiaSharp.NativeAssets.Linux.NoDependencies")]
    public void SkiaSharp_IsDirectlyReferencedOnlyByInfrastructureProject(string packageId)
    {
        Assert.Equal(
            ["src/Infrastructure/Infrastructure.csproj"],
            BackendProjects.DirectlyReferencing(packageId));
    }

    [Fact]
    public void InfrastructureComposition_RegistersEveryMediaPort()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=pt_manager_tests";

        services.AddInfrastructure(configuration);

        Assert.Equal(
            "Infrastructure.Media.Imaging.SkiaImageProcessor",
            Assert.Single(services, d => d.ServiceType == typeof(IImageProcessor)).ImplementationType?.FullName);
        Assert.Equal(
            "Infrastructure.Persistence.ClientPortal.MyAvatarStore",
            Assert.Single(services, d => d.ServiceType == typeof(IMyAvatarStore)).ImplementationType?.FullName);

        // Clientes HTTP tipados registam-se por fábrica; basta provar que existe
        // exatamente uma implementação para cada porta externa.
        Assert.Single(services, d => d.ServiceType == typeof(IMediaStorage));
        Assert.Single(services, d => d.ServiceType == typeof(IImageModerationService));
    }
}
