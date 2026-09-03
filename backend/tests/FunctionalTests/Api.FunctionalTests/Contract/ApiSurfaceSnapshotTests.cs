using System.Text;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Contract;

/// <summary>
/// Congela a superfície HTTP num ficheiro versionado e falha quando ela muda.
/// </summary>
public sealed class ApiSurfaceSnapshotTests : IDisposable
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    private const string SnapshotRelativePath = "docs/api/api-surface.v1.txt";

    private readonly ApiWebApplicationFactory _factory =
        new(UnusedConnectionString, "Development");

    [Fact]
    public async Task ApiSurface_MatchesVersionedSnapshot()
    {
        var current = await BuildSurfaceAsync();
        var snapshotPath = ResolveSnapshotPath();

        Assert.True(
            File.Exists(snapshotPath),
            $"Missing snapshot: {SnapshotRelativePath}. "
            + "Run RegenerateSnapshot and commit the file.");

        var expected = Normalize(await File.ReadAllTextAsync(
            snapshotPath, TestContext.Current.CancellationToken));

        Assert.Equal(expected, Normalize(current));
    }

    [Fact(Skip = "Run manually when accepting an intentional contract change.")]
    public async Task RegenerateSnapshot()
    {
        var current = await BuildSurfaceAsync();
        var snapshotPath = ResolveSnapshotPath();

        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        await File.WriteAllTextAsync(
            snapshotPath, current, TestContext.Current.CancellationToken);
    }

    private async Task<string> BuildSurfaceAsync()
    {
        using var client = _factory.CreateClient();
        using var stream = await client.GetStreamAsync(
            "/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream, cancellationToken: TestContext.Current.CancellationToken);

        var lines = new List<string>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var method = operation.Name.ToUpperInvariant();
                var parameters = DescribeParameters(operation.Value);
                var secured = operation.Value.TryGetProperty("security", out var security)
                    && security.GetArrayLength() > 0;

                lines.Add(
                    $"{method} {path.Name}{parameters} auth={(secured ? "required" : "anonymous")}");
            }
        }

        lines.Sort(StringComparer.Ordinal);

        var builder = new StringBuilder();
        foreach (var line in lines)
            builder.AppendLine(line);

        return builder.ToString();
    }

    private static string DescribeParameters(JsonElement operation)
    {
        if (!operation.TryGetProperty("parameters", out var parameters))
            return string.Empty;

        var described = new List<string>();
        foreach (var parameter in parameters.EnumerateArray())
        {
            var name = parameter.GetProperty("name").GetString();
            var location = parameter.GetProperty("in").GetString();
            var required = parameter.TryGetProperty("required", out var isRequired)
                && isRequired.GetBoolean();

            described.Add($"{location}:{name}{(required ? "!" : "?")}");
        }

        described.Sort(StringComparer.Ordinal);
        return described.Count == 0 ? string.Empty : $" [{string.Join(",", described)}]";
    }

    private static string ResolveSnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
            throw new InvalidOperationException(
                "Repository root not found from "
                + AppContext.BaseDirectory);

        return Path.Combine(directory.FullName, SnapshotRelativePath);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    public void Dispose() => _factory.Dispose();
}
