using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Observability;

/// <summary>
/// Em produção o EF Core não escreve cada comando SQL na consola: as métricas já dão a duração
/// e a contagem por rota. Em desenvolvimento o SQL continua visível.
/// </summary>
/// <remarks>
/// Lê os mesmos <c>appsettings</c> que a API copia para a saída e aplica-os como o host faz.
/// </remarks>
public sealed class ProductionLoggingConfigurationTests
{
    private const string SqlCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    [Fact]
    public void Production_HidesSqlCommandsButKeepsTheirFailures()
    {
        using var loggers = CreateLoggerFactory("Production");
        var sql = loggers.CreateLogger(SqlCommandCategory);

        Assert.False(sql.IsEnabled(LogLevel.Information));
        Assert.True(sql.IsEnabled(LogLevel.Warning));
        Assert.True(sql.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void Production_KeepsApplicationLogsAtInformation()
    {
        using var loggers = CreateLoggerFactory("Production");

        Assert.True(loggers.CreateLogger("Api.Controllers.AuthController").IsEnabled(LogLevel.Information));
        Assert.True(loggers.CreateLogger("Infrastructure.Jobs.JobDispatcher").IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void Development_StillShowsSqlCommands()
    {
        using var loggers = CreateLoggerFactory("Development");

        Assert.True(loggers.CreateLogger(SqlCommandCategory).IsEnabled(LogLevel.Information));
    }

    private static ILoggerFactory CreateLoggerFactory(string environment)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        return new ServiceCollection()
            .AddLogging(logging => logging
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddProvider(new AlwaysEnabledLoggerProvider()))
            .BuildServiceProvider()
            .GetRequiredService<ILoggerFactory>();
    }

    /// <summary>Provider que aceita tudo: o que decide o nível são só as regras do appsettings.</summary>
    private sealed class AlwaysEnabledLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new AlwaysEnabledLogger();

        public void Dispose() { }

        private sealed class AlwaysEnabledLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}
