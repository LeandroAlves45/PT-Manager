using Application.Common.Abstractions;
using Application.Features.Jobs.Abstractions;
using Application.Features.Jobs.Dispatching;
using Domain.Entities.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs;

/// <summary>Processa uma passagem limitada pelos durable jobs vencidos.</summary>
internal sealed class JobDispatcher
{
    private const string UnexpectedFailureCode = "job_handler_unexpected_failure";
    private const string TenantUnavailableCode = "job_tenant_unavailable";
    private const string HandlerMissingCode = "job_handler_not_registered";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobDispatchOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<JobDispatcher> _logger;

    public JobDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<JobDispatchOptions> options,
        IClock clock,
        ILogger<JobDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Reclama e processa no máximo o batch configurado.</summary>
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var remaining = _options.JobBatchSize;

        while (remaining > 0 && !cancellationToken.IsCancellationRequested)
        {
            var claimSize = Math.Min(remaining, _options.MaxConcurrencyPerDispatcher);
            var claimed = await ClaimAsync(claimSize, cancellationToken);
            if (claimed.Count == 0)
                return;

            remaining -= claimed.Count;
            await Task.WhenAll(claimed.Select(job =>
                ProccessAsync(job, cancellationToken)));
        }
    }

    private async Task<IReadOnlyList<DurableJob>> ClaimAsync(
        int claimSize,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDurableJobStore>();
        return await store.ClaimDueJobsAsync(
            _options.LeaseDuration,
            claimSize,
            cancellationToken);
    }

    private async Task ProccessAsync(
        DurableJob claimed,
        CancellationToken activationCancellationToken)
    {
        if (!claimed.LeaseOwnerId.HasValue)
            throw new InvalidOperationException("A claimed job must have a lease owner.");

        var envelope = new DurableJobEnvelope(
            claimed.Id,
            claimed.TrainerId,
            claimed.JobType,
            claimed.JobVersion,
            claimed.Payload,
            claimed.IdempotencyKey,
            claimed.CorrelationId,
            claimed.Attempts,
            claimed.LeaseOwnerId.Value);

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = envelope.Id,
            ["JobType"] = envelope.JobType,
            ["JobVersion"] = envelope.JobVersion,
            ["JobAttempt"] = envelope.Attempts,
            ["CorrelationId"] = envelope.CorrelationId,
            ["TrainerId"] = envelope.TrainerId,
        });

        DispatchItemOutcome outcome;
        try
        {
            outcome = await ExecuteHandlerAsync(envelope, activationCancellationToken);
        }
        catch (OperationCanceledException) when (activationCancellationToken.IsCancellationRequested)
        {
            // O lease permanece em Processing e será recuperado depois de expirar.
            return;
        }
        catch (Exception exception)
        {
            // O tipo de exceção ajuda o diagnóstico sem persistir a mensagem, o
            // stack trace ou dados potencialmente provenientes do provider.
            _logger.LogError(
                JobDispatchLogEvents.ActivationFailure,
                "Durable job handler failed unexpectedly with failure type {FailureType}.",
                exception.GetType().Name);
            outcome = DispatchItemOutcome.TransientFailure(UnexpectedFailureCode);
        }

        await ApplyOutcomeAsync(envelope, outcome, activationCancellationToken);
    }

    private async Task<DispatchItemOutcome> ExecuteHandlerAsync(
        DurableJobEnvelope job,
        CancellationToken activationCancellationToken)
    {
        await using var itemScope = _scopeFactory.CreateAsyncScope();
        var handlers = itemScope.ServiceProvider
            .GetServices<IDurableJobHandler>()
            .Where(handler =>
                handler.JobType == job.JobType &&
                handler.JobVersion == job.JobVersion)
            .Take(2)
            .ToArray();

        if (handlers.Length == 0)
            return DispatchItemOutcome.PermanentFailure(HandlerMissingCode);

        if (handlers.Length > 1)
            throw new InvalidOperationException(
                "More than one durable job handler is registered for the same route.");

        var tenantValidator = itemScope.ServiceProvider.GetRequiredService<JobTenantValidator>();
        if (!await tenantValidator.IsAvailableAsync(job.TrainerId, activationCancellationToken))
        {
            _logger.LogWarning(
                JobDispatchLogEvents.TenantRejected,
                "Durable job tenant was rejected before handler execution.");
            return DispatchItemOutcome.PermanentFailure(TenantUnavailableCode);
        }

        var tenantInitializer = itemScope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>();
        tenantInitializer.Establish(
            job.TrainerId,
            userId: null,
            role: null,
            TenantOrigin.Job,
            isAdministrative: false);

        await using var heartbeat = LeaseHeartbeat.Start(
            token => RenewLeaseAsync(job.Id, job.LeaseOwnerId, token),
            _options.LeaseRenewalInterval,
            activationCancellationToken);

        DispatchItemOutcome outcome;
        try
        {
            outcome = await handlers[0].HandleAsync(job, heartbeat.ProcessingToken);
        }
        catch (OperationCanceledException) when (heartbeat.LeaseLost)
        {
            outcome = DispatchItemOutcome.LeaseLost();
        }

        await heartbeat.StopAsync();
        return heartbeat.LeaseLost ? DispatchItemOutcome.LeaseLost() : outcome;
    }

    private async Task<bool> RenewLeaseAsync(
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDurableJobStore>();
        return await store.TryRenewLeaseAsync(
            jobId,
            leaseOwnerId,
            _options.LeaseDuration,
            cancellationToken);
    }

    private async Task ApplyOutcomeAsync(
        DurableJobEnvelope job,
        DispatchItemOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (outcome.Kind == DispatchItemOutcomeKind.LeaseLost)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.LeaseLost,
                "Durable job processing stopped because its lease was lost.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDurableJobStore>();

        bool updated;
        if (outcome.Kind == DispatchItemOutcomeKind.Succeeded)
        {
            updated = await store.TryCompleteAsync(
                job.Id,
                job.LeaseOwnerId,
                cancellationToken);
            if (updated)
                _logger.LogInformation(
                    JobDispatchLogEvents.ItemSucceeded,
                    "Durable job completed successfully.");
        }
        else
        {
            var nextAttemptAt = outcome.Kind == DispatchItemOutcomeKind.TransientFailure
                ? RetryScheduleCalculator.CalculateNextAttempt(
                    _clock.UtcNow,
                    job.Attempts,
                    job.IdempotencyKey,
                    _options)
                : null;

            updated = await store.TryRecordFailureAsync(
                job.Id,
                job.LeaseOwnerId,
                outcome.FailureCode!,
                nextAttemptAt,
                cancellationToken);

            if (updated && nextAttemptAt.HasValue)
            {
                _logger.LogWarning(
                    JobDispatchLogEvents.ItemRetryScheduled,
                    "Durable job scheduled for retry with failure code {FailureCode}.",
                    outcome.FailureCode);
            }
            else if (updated)
            {
                _logger.LogError(
                    JobDispatchLogEvents.ItemDeadLettered,
                    "Durable job moved to dead letter with failure code {FailureCode}.",
                    outcome.FailureCode);
            }
        }

        if (!updated)
            _logger.LogWarning(
                JobDispatchLogEvents.LeaseLost,
                "Durable job final transition was rejected because its lease was lost.");
    }
}
