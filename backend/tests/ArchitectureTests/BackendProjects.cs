namespace ArchitectureTests;

/// <summary>
/// Localiza os ficheiros de projeto do backend para regras que inspecionam
/// referências de pacotes diretamente nos <c>.csproj</c>.
/// </summary>
/// <remarks>
/// Extraído quando uma segunda regra (SkiaSharp) passou a precisar da mesma
/// varredura que a regra do Stripe. Duplicado, um ajuste à exclusão de
/// <c>obj</c> num dos lados deixaria o outro a produzir falsos resultados.
/// </remarks>
internal static class BackendProjects
{
    internal static string ResolveBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PTManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Backend root was not found.");
    }

    /// <summary>
    /// Devolve, relativos à raiz do backend, os projetos que referenciam
    /// diretamente o pacote indicado. Ignora cópias geradas em <c>obj</c>.
    /// </summary>
    internal static string[] DirectlyReferencing(string packageId)
    {
        var backendRoot = ResolveBackendRoot();
        return Directory
            .EnumerateFiles(backendRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                $"PackageReference Include=\"{packageId}\"",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(backendRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
