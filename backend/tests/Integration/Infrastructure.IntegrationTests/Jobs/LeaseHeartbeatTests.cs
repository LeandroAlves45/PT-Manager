using Infrastructure.Jobs;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica o heartbeat que mantém um lease vivo.
/// Só o owner vivo pode continuar a trabalhar. Quando a renovação falha ou o
/// resultado é desconhecido, o token de processamento tem de ser cancelado: um
/// handler que continuasse a correr poderia concluir trabalho já reclamado por
/// outro worker.
/// </summary>
public sealed class LeaseHeartbeatTests
{
    private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task Heartbeat_WhileRenewalSucceeds_KeepsProcessingTokenAlive()
    {
        var renewals = 0;

        await using var heartbeat = LeaseHeartbeat.Start(
            _ => { Interlocked.Increment(ref renewals); return Task.FromResult(true); },
            FastInterval,
            CancellationToken.None);

        await WaitUntilAsync(() => Volatile.Read(ref renewals) >= 2);

        Assert.False(heartbeat.LeaseLost);
        Assert.False(heartbeat.ProcessingToken.IsCancellationRequested);

        await heartbeat.StopAsync();
    }

    [Fact]
    public async Task Heartbeat_WhenRenewalReturnsFalse_CancelsProcessingToken()
    {
        await using var heartbeat = LeaseHeartbeat.Start(
            _ => Task.FromResult(false),
            FastInterval,
            CancellationToken.None);

        await WaitUntilAsync(() => heartbeat.ProcessingToken.IsCancellationRequested);

        // Perder o lease tem de interromper o trabalho em curso.
        Assert.True(heartbeat.LeaseLost);
    }

    [Fact]
    public async Task Heartbeat_WhenRenewalThrows_TreatsUnknownStateAsLeaseLost()
    {
        await using var heartbeat = LeaseHeartbeat.Start(
            _ => throw new InvalidOperationException("database unreachable"),
            FastInterval,
            CancellationToken.None);

        await WaitUntilAsync(() => heartbeat.ProcessingToken.IsCancellationRequested);

        // Não saber se a renovação passou é equivalente a não a ter.
        Assert.True(heartbeat.LeaseLost);
    }

    [Fact]
    public async Task Heartbeat_WhenCallerCancels_StopsWithoutDeclaringLeaseLost()
    {
        using var activation = new CancellationTokenSource();

        await using var heartbeat = LeaseHeartbeat.Start(
            _ => Task.FromResult(true),
            FastInterval,
            activation.Token);

        await activation.CancelAsync();

        // Um shutdown do caller não é perda de ownership.
        Assert.True(heartbeat.ProcessingToken.IsCancellationRequested);
        Assert.False(heartbeat.LeaseLost);

        await heartbeat.StopAsync();
    }

    [Fact]
    public async Task StopAsync_IsIdempotentAndStopsFurtherRenewals()
    {
        var renewals = 0;

        var heartbeat = LeaseHeartbeat.Start(
            _ => { Interlocked.Increment(ref renewals); return Task.FromResult(true); },
            FastInterval,
            CancellationToken.None);

        await WaitUntilAsync(() => Volatile.Read(ref renewals) >= 1);
        await heartbeat.StopAsync();
        var afterStop = Volatile.Read(ref renewals);

        await heartbeat.StopAsync();
        await Task.Delay(FastInterval * 4, TestContext.Current.CancellationToken);

        Assert.Equal(afterStop, Volatile.Read(ref renewals));
        await heartbeat.DisposeAsync();
    }

    [Fact]
    public void Start_RejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LeaseHeartbeat.Start(
            _ => Task.FromResult(true), TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public void Start_RejectsNullRenewal()
    {
        Assert.Throws<ArgumentNullException>(() => LeaseHeartbeat.Start(
            null!, FastInterval, CancellationToken.None));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Fail("Condition was not met before the timeout.");
    }
}
