using System.Reflection;

namespace ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly =
        typeof(Domain.Entities.Clients.Client).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(Application.Common.Abstractions.ITenantContext).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(Infrastructure.Data.PtManagerDbContext).Assembly;

    private static readonly Assembly ApiAssembly = Assembly.Load("Api");

    [Fact]
    public void Domain_DoesNotReferenceApplication()
    {
        Assert.DoesNotContain(
            DomainAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Application");
    }

    [Fact]
    public void Domain_DoesNotReferenceInfrastructure()
    {
        Assert.DoesNotContain(
            DomainAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Infrastructure");
    }

    [Fact]
    public void Domain_DoesNotReferenceApi()
    {
        Assert.DoesNotContain(
            DomainAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Api");
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Infrastructure");
    }

    [Fact]
    public void Application_DoesNotReferenceApi()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Api");
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        Assert.DoesNotContain(
            InfrastructureAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "Api");
    }

    [Theory]
    [InlineData("AutoMapper")]
    [InlineData("MediatR")]
    public void SolutionLayers_DoNotReferenceForbiddenDependency(string dependencyName)
    {
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies()
            .Concat(ApplicationAssembly.GetReferencedAssemblies())
            .Concat(InfrastructureAssembly.GetReferencedAssemblies())
            .Concat(ApiAssembly.GetReferencedAssemblies());

        Assert.DoesNotContain(
            referencedAssemblies,
            reference => reference.Name == dependencyName);
    }
}
