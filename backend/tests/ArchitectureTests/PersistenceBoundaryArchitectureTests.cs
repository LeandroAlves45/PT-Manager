using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace ArchitectureTests;

/// <summary>
/// Fronteiras de persistência: o acesso a dados e o isolamento por tenant vivem na infraestrutura.
/// </summary>
public sealed class PersistenceBoundaryArchitectureTests
{
    private static readonly Assembly DomainAssembly =
        typeof(Domain.Entities.Clients.Client).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(Application.Common.Abstractions.ITenantContext).Assembly;

    private static readonly Assembly ApiAssembly = Assembly.Load("Api");

    private const string EntityFrameworkAssemblyPrefix = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void Controllers_DoNotDependOnTheDbContextOrEntityFramework()
    {
        var controllers = ApiAssembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(controllers);

        var offenders = controllers
            .Where(controller => DependencyTypes(controller).Any(IsPersistenceType))
            .Select(controller => controller.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    public void DomainAndApplication_DoNotReferenceEntityFramework(string layer)
    {
        var assembly = layer == "Domain" ? DomainAssembly : ApplicationAssembly;

        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith(EntityFrameworkAssemblyPrefix, StringComparison.Ordinal) == true);
    }

    [Fact]
    public void IgnoreQueryFilters_IsUsedOnlyInsideTheInfrastructureProject()
    {
        var sourceRoot = Path.Combine(BackendProjects.ResolveBackendRoot(), "src");
        var infrastructureRoot = Path.Combine(sourceRoot, "Infrastructure") + Path.DirectorySeparatorChar;

        var offenders = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .Where(path => !path.StartsWith(infrastructureRoot, StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("IgnoreQueryFilters", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> DependencyTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        return type.GetConstructors(all)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Concat(type.GetFields(all).Select(field => field.FieldType))
            .Concat(type.GetProperties(all).Select(property => property.PropertyType));
    }

    private static bool IsPersistenceType(Type type) =>
        (type.Namespace?.StartsWith(EntityFrameworkAssemblyPrefix, StringComparison.Ordinal) == true)
        || type.Name.EndsWith("DbContext", StringComparison.Ordinal);

    private static bool IsGenerated(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase);
    }
}
