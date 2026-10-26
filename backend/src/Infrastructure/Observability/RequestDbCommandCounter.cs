using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Observability;

/// <summary>Conta os comandos SQL executados dentro do pedido HTTP atual.</summary>
/// <remarks>
/// O middleware da Api abre o contador no início do pedido e lê-o no fim. O
/// <see cref="AsyncLocal{T}"/> segue o fluxo assíncrono do pedido até ao DbContext, que é
/// scoped; fora de um pedido (dispatcher, migrations) não há contador e o incremento é
/// ignorado.
/// </remarks>
public static class RequestDbCommandCounter
{
    private static readonly AsyncLocal<StrongBox<int>?> Current = new();

    /// <summary>Abre um contador novo para o fluxo atual e devolve-o.</summary>
    public static StrongBox<int> Start()
    {
        var counter = new StrongBox<int>(0);
        Current.Value = counter;
        return counter;
    }

    internal static void Increment()
    {
        var counter = Current.Value;
        if (counter is not null)
            Interlocked.Increment(ref counter.Value);
    }
}

/// <summary>Incrementa <see cref="RequestDbCommandCounter"/> por cada comando enviado.</summary>
/// <remarks>Singleton e sem estado: o estado vive no contador do pedido.</remarks>
public sealed class DbCommandMetricsInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        RequestDbCommandCounter.Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        RequestDbCommandCounter.Increment();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        RequestDbCommandCounter.Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RequestDbCommandCounter.Increment();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        RequestDbCommandCounter.Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        RequestDbCommandCounter.Increment();
        return ValueTask.FromResult(result);
    }
}
