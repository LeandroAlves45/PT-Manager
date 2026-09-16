using System.Reflection;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

/// <summary>
/// Congela a fronteira dos vídeos geridos: AWS SDK, R2 e o leitor de containers
/// existem só em Infrastructure; a Application vê apenas as portas; cada controller
/// fixa o seu catálogo e a sua política de autorização.
/// </summary>
public sealed class VideoArchitectureTests
{
    private static readonly Assembly ApplicationAssembly = typeof(IVideoObjectStorage).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Theory]
    [InlineData("AWSSDK.S3")]
    [InlineData("AWSSDK.Core")]
    public void Application_DoesNotReferenceTheAwsSdk(string assemblyName)
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == assemblyName);
    }

    [Fact]
    public void ProviderTypes_LiveOnlyInInfrastructure()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetTypes(),
            type => type.Name.StartsWith("R2", StringComparison.Ordinal) ||
                type.FullName!.Contains("Amazon", StringComparison.Ordinal) ||
                type.FullName.Contains("IsoBmff", StringComparison.Ordinal));
    }

    [Fact]
    public void AwsSdk_IsDirectlyReferencedOnlyByInfrastructureProject()
    {
        Assert.Equal(
            ["src/Infrastructure/Infrastructure.csproj"],
            BackendProjects.DirectlyReferencing("AWSSDK.S3"));
    }

    [Theory]
    [InlineData(typeof(IVideoObjectStorage))]
    [InlineData(typeof(IVideoMetadataProbe))]
    [InlineData(typeof(IExerciseVideoStore))]
    [InlineData(typeof(IExerciseVideoQueries))]
    [InlineData(typeof(IExerciseVideoProcessingStore))]
    public void VideoPorts_AreDeclaredInApplication(Type port)
    {
        Assert.Equal(ApplicationAssembly, port.Assembly);
        Assert.True(port.IsInterface);
    }

    [Fact]
    public void InfrastructureComposition_RegistersEveryVideoPortOnce()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=pt_manager_tests";

        services.AddInfrastructure(configuration);

        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(services, d => d.ServiceType == typeof(IVideoObjectStorage)).Lifetime);
        Assert.Single(services, d => d.ServiceType == typeof(IVideoMetadataProbe));
        Assert.Equal(
            "Infrastructure.Persistence.Training.ExerciseVideoStore",
            Assert.Single(services, d => d.ServiceType == typeof(IExerciseVideoStore)).ImplementationType?.FullName);
        Assert.Equal(
            "Infrastructure.Persistence.Training.ExerciseVideoProcessingStore",
            Assert.Single(services, d => d.ServiceType == typeof(IExerciseVideoProcessingStore)).ImplementationType?.FullName);
    }

    [Theory]
    [InlineData("Api.Controllers.ExerciseVideosController", "trainer", false)]
    [InlineData("Api.Controllers.GlobalExerciseVideosController", "superuser", true)]
    public void ManagedVideoControllers_FixTheirPolicyInsteadOfTrustingTheRequest(
        string controllerName, string policyFragment, bool administrative)
    {
        var controller = ApiAssembly.GetType(controllerName);

        Assert.NotNull(controller);
        Assert.Equal("ManagedExerciseVideoControllerBase", controller!.BaseType!.Name);
        var policies = controller.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ToArray();
        Assert.Contains(policies, policy => policy!.Contains(policyFragment, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            administrative,
            controller.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "AdministrativeContextAttribute"));
    }

    [Fact]
    public void CompleteUpload_UsesTheSameRateLimitAsRequestingAnUpload()
    {
        var method = typeof(Api.Controllers.ManagedExerciseVideoControllerBase)
            .GetMethod(nameof(Api.Controllers.ManagedExerciseVideoControllerBase.CompleteUploadAsync));

        Assert.NotNull(method);
        var policy = Assert.Single(method!.GetCustomAttributes<EnableRateLimitingAttribute>()).PolicyName;
        Assert.Equal("video_upload", policy);
    }
}
