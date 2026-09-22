using Api.FunctionalTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Configuration;

/// <summary>
/// Prova que o host escreve stdout em JSON com timestamps UTC (QG6C-HEALTH-001, doc 14).
/// </summary>
/// <remarks>
/// Lê a configuração efetiva do host em vez de capturar <c>Console.Out</c>: a consola é
/// global ao processo e os testes correm em paralelo, o que tornaria a captura instável.
/// </remarks>
public sealed class StructuredLoggingTests : IDisposable
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    private readonly ApiWebApplicationFactory _factory = new(UnusedConnectionString);

    [Fact]
    public void ConsoleLogger_UsesJsonFormatterWithUtcTimestamps()
    {
        var console = _factory.Services
            .GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>()
            .CurrentValue;
        var json = _factory.Services
            .GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>()
            .CurrentValue;

        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
        Assert.True(json.UseUtcTimestamp);
        Assert.Equal("yyyy-MM-ddTHH:mm:ss.fffZ", json.TimestampFormat);
        Assert.Contains(
            _factory.Services.GetServices<ILoggerProvider>(),
            provider => provider is ConsoleLoggerProvider);
    }

    public void Dispose() => _factory.Dispose();
}
