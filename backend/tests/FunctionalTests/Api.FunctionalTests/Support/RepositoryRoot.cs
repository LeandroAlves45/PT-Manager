namespace Api.FunctionalTests.Support;

/// <summary>Localiza a raiz do repositório Git a partir de um diretório de saída.</summary>
/// <remarks>
/// Num checkout normal <c>.git</c> é um diretório; numa worktree é um ficheiro que aponta
/// para o repositório principal. Aceitar só o diretório fazia o teste de snapshot falhar
/// em qualquer worktree, embora o ficheiro versionado estivesse lá.
/// </remarks>
internal static class RepositoryRoot
{
    /// <summary>Sobe a partir de <paramref name="startDirectory"/> até encontrar <c>.git</c>.</summary>
    /// <exception cref="InvalidOperationException">Nenhum antecessor contém <c>.git</c>.</exception>
    public static string Find(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        for (var directory = new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
                return directory.FullName;
        }

        throw new InvalidOperationException(
            $"Repository root not found: no '.git' directory or file above '{startDirectory}'.");
    }
}
