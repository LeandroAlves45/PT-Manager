using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Jobs;

/// <summary>Calcula o próximo instante de retry sem estado global aleatório.</summary>
internal static class RetryScheduleCalculator
{
    /// <summary>Devolve null quando o número máximo de tentativas foi consumido.</summary>
    public static DateTime? CalculateNextAttempt(
        DateTime now,
        int attempts,
        string idempotencyKey,
        JobDispatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Retry calculation requires UTC.", nameof(now));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        if (attempts >= options.MaxAttempts)
            return null;

        var exponent = Math.Min(attempts - 1, 30);
        var exponentialTicks = options.InitialRetryDelay.Ticks * Math.Pow(2, exponent);
        var cappedTicks = Math.Min(exponentialTicks, options.MaximumRetryDelay.Ticks);
        var jitterRatio = CalculateJitterRatio(
            idempotencyKey,
            attempts,
            options.MaximumJitterRatio);

        var delayTicks = checked((long)Math.Min(
            cappedTicks * (1d + jitterRatio),
            options.MaximumRetryDelay.Ticks));

        return now.AddTicks(delayTicks);
    }

    private static double CalculateJitterRatio(
        string idempotencyKey,
        int attempts,
        double maximumJitterRatio)
    {
        if (maximumJitterRatio == 0)
            return 0;

        var input = Encoding.UTF8.GetBytes($"{idempotencyKey}:{attempts}");
        var hash = SHA256.HashData(input);
        var sample = BinaryPrimitives.ReadUInt32BigEndian(hash);
        var normalized = sample / (double)uint.MaxValue;
        return normalized * maximumJitterRatio;
    }
}
