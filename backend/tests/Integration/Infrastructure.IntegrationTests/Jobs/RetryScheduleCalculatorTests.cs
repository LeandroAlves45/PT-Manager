using Infrastructure.Jobs;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica o cálculo de retry.
/// O retry tem de ser reproduzível para diagnóstico, mas não pode
/// sincronizar todos os workers no mesmo instante.
/// O jitter é derivado da idempotency key em vez de um gerador aleatório global
/// justamente para satisfazer as duas exigências ao mesmo tempo.
/// </summary>
public sealed class RetryScheduleCalculatorTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateNextAttempt_WhenAttemptsExhausted_ReturnsNull()
    {
        var options = CreateOptions(maxAttempts: 5);

        // Esgotar as tentativas tem de produzir dead letter, não mais um retry.
        Assert.Null(RetryScheduleCalculator.CalculateNextAttempt(Now, 5, "key", options));
        Assert.Null(RetryScheduleCalculator.CalculateNextAttempt(Now, 6, "key", options));
    }

    [Fact]
    public void CalculateNextAttempt_LastAllowedAttempt_StillSchedulesRetry()
    {
        var options = CreateOptions(maxAttempts: 5);

        Assert.NotNull(RetryScheduleCalculator.CalculateNextAttempt(Now, 4, "key", options));
    }

    [Fact]
    public void CalculateNextAttempt_GrowsExponentiallyAcrossAttempts()
    {
        var options = CreateOptions(maxAttempts: 10, jitter: 0);

        var first = Delay(1, options);
        var second = Delay(2, options);
        var third = Delay(3, options);

        Assert.Equal(TimeSpan.FromMinutes(1), first);
        Assert.Equal(TimeSpan.FromMinutes(2), second);
        Assert.Equal(TimeSpan.FromMinutes(4), third);
    }

    [Fact]
    public void CalculateNextAttempt_NeverExceedsConfiguredMaximum()
    {
        var options = CreateOptions(maxAttempts: 20, jitter: 0.5);

        // Sem o cap, o crescimento exponencial ultrapassaria rapidamente qualquer
        // janela operacional aceitável.
        for (var attempt = 1; attempt < 20; attempt++)
            Assert.True(Delay(attempt, options) <= options.MaximumRetryDelay);
    }

    [Fact]
    public void CalculateNextAttempt_IsDeterministicForTheSameKeyAndAttempt()
    {
        var options = CreateOptions();

        var first = RetryScheduleCalculator.CalculateNextAttempt(Now, 2, "stable-key", options);
        var second = RetryScheduleCalculator.CalculateNextAttempt(Now, 2, "stable-key", options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void CalculateNextAttempt_DifferentKeysSpreadTheRetryWindow()
    {
        var options = CreateOptions();

        var delays = Enumerable.Range(0, 40)
            .Select(index => RetryScheduleCalculator.CalculateNextAttempt(
                Now, 2, $"key-{index}", options))
            .Distinct()
            .Count();

        // Se todas as chaves produzissem o mesmo atraso, o jitter não estaria a
        // dessincronizar nada e um pico de falhas repetir-se-ia em bloco.
        Assert.True(delays > 1, "Jitter must distribute retries across keys.");
    }

    [Fact]
    public void CalculateNextAttempt_WithoutJitter_UsesExactExponentialDelay()
    {
        var options = CreateOptions(jitter: 0);

        Assert.Equal(Now.AddMinutes(1),
            RetryScheduleCalculator.CalculateNextAttempt(Now, 1, "key", options));
    }

    [Fact]
    public void CalculateNextAttempt_JitterOnlyExtendsWithinConfiguredRatio()
    {
        var options = CreateOptions(jitter: 0.20);

        for (var index = 0; index < 50; index++)
        {
            var delay = Delay(1, options, $"key-{index}");

            Assert.True(delay >= TimeSpan.FromMinutes(1));
            Assert.True(delay <= TimeSpan.FromMinutes(1) * 1.20);
        }
    }

    [Fact]
    public void CalculateNextAttempt_RejectsNonUtcClock()
    {
        var options = CreateOptions();
        var local = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(
            () => RetryScheduleCalculator.CalculateNextAttempt(local, 1, "key", options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateNextAttempt_RejectsNonPositiveAttempts(int attempts)
    {
        var options = CreateOptions();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RetryScheduleCalculator.CalculateNextAttempt(Now, attempts, "key", options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CalculateNextAttempt_RejectsBlankIdempotencyKey(string key)
    {
        var options = CreateOptions();

        Assert.Throws<ArgumentException>(
            () => RetryScheduleCalculator.CalculateNextAttempt(Now, 1, key, options));
    }

    [Fact]
    public void CalculateNextAttempt_RejectsNullOptions()
    {
        Assert.Throws<ArgumentNullException>(
            () => RetryScheduleCalculator.CalculateNextAttempt(Now, 1, "key", null!));
    }

    private static TimeSpan Delay(
        int attempts,
        JobDispatchOptions options,
        string key = "key") =>
        RetryScheduleCalculator.CalculateNextAttempt(Now, attempts, key, options)!.Value - Now;

    private static JobDispatchOptions CreateOptions(
        int maxAttempts = 5,
        double jitter = 0.20) =>
        new()
        {
            MaxAttempts = maxAttempts,
            InitialRetryDelay = TimeSpan.FromMinutes(1),
            MaximumRetryDelay = TimeSpan.FromHours(1),
            MaximumJitterRatio = jitter
        };
}
