using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Support;

/// <summary>Captura todas as representações pelas quais um segredo pode chegar a um sink.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<CapturedLogEntry> _entries = [];
    private readonly Lock _gate = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public IReadOnlyList<CapturedLogEntry> Snapshot()
    {
        lock (_gate)
            return [.. _entries];
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
    }

    public void Dispose() { }

    private void Add(CapturedLogEntry entry)
    {
        lock (_gate)
            _entries.Add(entry);
    }

    private sealed class CapturingLogger(
        string categoryName,
        CapturingLoggerProvider owner) : ILogger
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
            var structuredState = state as IEnumerable<KeyValuePair<string, object?>>;
            owner.Add(new CapturedLogEntry(
                categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                structuredState?.ToDictionary(pair => pair.Key, pair => pair.Value)
                    ?? new Dictionary<string, object?>(),
                exception));
        }
    }
}

internal sealed record CapturedLogEntry(
    string CategoryName,
    LogLevel LogLevel,
    EventId EventId,
    string FormattedMessage,
    IReadOnlyDictionary<string, object?> State,
    Exception? Exception);
