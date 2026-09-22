using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Contract;

/// <summary>
/// Prova que o snapshot contratual é encontrado num checkout normal e numa worktree (doc 15).
/// </summary>
public sealed class RepositoryRootTests : IDisposable
{
    private readonly DirectoryInfo _sandbox =
        Directory.CreateTempSubdirectory("ptmanager-repository-root-");

    [Fact]
    public void Find_WithGitDirectory_ReturnsCheckoutRoot()
    {
        Directory.CreateDirectory(Path.Combine(_sandbox.FullName, ".git"));
        var output = CreateOutputDirectory();

        Assert.Equal(_sandbox.FullName, RepositoryRoot.Find(output));
    }

    [Fact]
    public void Find_WithGitFile_ReturnsWorktreeRoot()
    {
        File.WriteAllText(
            Path.Combine(_sandbox.FullName, ".git"),
            "gitdir: /repositories/main/.git/worktrees/feature");
        var output = CreateOutputDirectory();

        Assert.Equal(_sandbox.FullName, RepositoryRoot.Find(output));
    }

    [Fact]
    public void Find_WithoutGitMarker_FailsWithStartDirectory()
    {
        var output = CreateOutputDirectory();

        var exception = Assert.Throws<InvalidOperationException>(() => RepositoryRoot.Find(output));

        Assert.Contains(output, exception.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _sandbox.Delete(recursive: true);

    private string CreateOutputDirectory() =>
        Directory.CreateDirectory(
            Path.Combine(_sandbox.FullName, "backend", "tests", "bin", "Debug", "net10.0")).FullName;
}
