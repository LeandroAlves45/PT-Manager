using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Conta os comandos SQL executados dentro de um âmbito, para provar orçamentos de
/// queries por caso de uso.
/// </summary>
public sealed class CommandCountingInterceptor : DbCommandInterceptor
{
    private static readonly AsyncLocal<CommandCountingScope?> Current = new();

    /// <summary>Abre um âmbito de contagem para a operação seguinte.</summary>
    public static CommandCountingScope BeginScope()
    {
        var scope = new CommandCountingScope(() => Current.Value = null);
        Current.Value = scope;
        return scope;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Record(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Record(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void Record(DbCommand command) =>
        Current.Value?.Add(command.CommandText);
}

/// <summary>Âmbito de contagem de comandos SQL.</summary>
public sealed class CommandCountingScope : IDisposable
{
    private readonly ConcurrentQueue<string> _commands = new();
    private readonly Action _release;

    internal CommandCountingScope(Action release) => _release = release;

    /// <summary>Número de comandos SQL executados desde a abertura do âmbito.</summary>
    public int Count => _commands.Count;

    /// <summary>Texto dos comandos executados, pela ordem de execução.</summary>
    public IReadOnlyList<string> Commands => _commands.ToArray();

    internal void Add(string commandText) => _commands.Enqueue(commandText);

    /// <summary>Fecha o âmbito e deixa de contar.</summary>
    public void Dispose() => _release();
}
