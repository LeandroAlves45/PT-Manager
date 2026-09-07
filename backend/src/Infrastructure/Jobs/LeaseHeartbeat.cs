namespace Infrastructure.Jobs;

/// <summary>Mantém um lease vivo enquanto um efeito está em execução.</summary>
internal sealed class LeaseHeartbeat : IAsyncDisposable
{
    private readonly CancellationTokenSource _stopSource;
    private readonly CancellationTokenSource _processingSource;
    private readonly Task _heartbeatTask;
    private int _stopped;

    private LeaseHeartbeat(
        Func<CancellationToken, Task<bool>> renew,
        TimeSpan interval,
        CancellationToken processingCancellationToken)
    {
        _stopSource = new CancellationTokenSource();
        _processingSource = CancellationTokenSource.CreateLinkedTokenSource(
            processingCancellationToken);
        _heartbeatTask = RunAsync(renew, interval);
    }

    /// <summary>Token cancelado pelo caller ou pela perda do lease.</summary>
    public CancellationToken ProcessingToken => _processingSource.Token;

    /// <summary>Indica que a renovação falhou ou devolveu false.</summary>
    public bool LeaseLost { get; private set; }

    /// <summary>Inicia o heartbeat imediatamente associado ao processamento.</summary>
    public static LeaseHeartbeat Start(
        Func<CancellationToken, Task<bool>> renew,
        TimeSpan interval,
        CancellationToken processingCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(renew);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interval.TotalSeconds);

        return new LeaseHeartbeat(renew, interval, processingCancellationToken);
    }

    /// <summary>Pára novas renovações e aguarda a renovação em curso.</summary>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        await _stopSource.CancelAsync();
        try
        {
            await _heartbeatTask;
        }
        catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
        {
            // O cancelamento é o mecanismo normal usado para terminar o timer.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _processingSource.Dispose();
        _stopSource.Dispose();
    }

    private async Task RunAsync(
        Func<CancellationToken, Task<bool>> renew,
        TimeSpan interval)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(_stopSource.Token))
            {
                bool renewed;
                try
                {
                    renewed = await renew(_stopSource.Token);
                }
                catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // Não conhecer o estado da renovação equivale a perder o lease.
                    // Continuar permitiria concluir o trabalho com ownership inválido.
                    renewed = false;
                }

                if (renewed)
                    continue;

                LeaseLost = true;
                await _processingSource.CancelAsync();
                return;
            }
        }
        catch (OperationCanceledException) when (_stopSource.IsCancellationRequested)
        {
            // O caller terminou o trabalho antes da próxima renovação.
        }
    }
}
