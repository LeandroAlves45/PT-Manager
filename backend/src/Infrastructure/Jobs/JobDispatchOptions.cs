namespace Infrastructure.Jobs;

/// <summary>Limites operacionais dos dispatchers PostgresSQL.</summary>
public sealed class JobDispatchOptions
{
    public const string SectionName = "JobDispatch";
    public int JobBatchSize { get; set; } = 20;
    public int OutboxBatchSize { get; set; } = 20;
    public int MaxConcurrencyPerDispatcher { get; set; } = 2;
    public TimeSpan ActivationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(20);
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromHours(1);
    public double MaximumJitterRatio { get; set; } = 0.20;

    /// <summary>Valida limites que impedem claims sem capacidade de execução.</summary>
    public bool IsValid() =>
        JobBatchSize is > 0 and <= 100 &&
        OutboxBatchSize is > 0 and <= 100 &&
        MaxConcurrencyPerDispatcher is > 0 and <= 10 &&
        ActivationTimeout >= TimeSpan.FromSeconds(5) &&
        ActivationTimeout <= TimeSpan.FromMinutes(5) &&
        LeaseDuration > ActivationTimeout &&
        LeaseRenewalInterval > TimeSpan.Zero &&
        LeaseRenewalInterval <= LeaseDuration / 2 &&
        MaxAttempts is > 0 and <= 20 &&
        InitialRetryDelay > TimeSpan.Zero &&
        MaximumRetryDelay >= InitialRetryDelay &&
        MaximumRetryDelay <= TimeSpan.FromDays(1) &&
        MaximumJitterRatio is >= 0 and <= 0.50;
}
