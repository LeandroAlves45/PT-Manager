using System.Diagnostics;
using Application.Common.Abstractions;
using Application.Common.Observability;
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
    private const string Queue = "job";

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
        await DispatchBatchesAsync(cancellationToken);

        if (!cancellationToken.IsCancellationRequested)
            await RecordBacklogAsync(cancellationToken);
    }

    private async Task DispatchBatchesAsync(CancellationToken cancellationToken)
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
                ProcessAsync(job, cancellationToken)));
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
            _options.MaxAttempts,
            cancellationToken);
    }

    private async Task ProcessAsync(
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

        // Uma span por item, filha do pedido QStash que acordou a API. O correlation id do
        // job é o trace id do pedido que o criou: liga as duas pontas no Grafana.
        using var activity = PtManagerTelemetry.ActivitySource.StartActivity(
            "durable_job process",
            ActivityKind.Consumer);
        activity?.SetTag("ptm.queue", Queue);
        activity?.SetTag("ptm.item.type", envelope.JobType);
        activity?.SetTag("ptm.item.attempt", envelope.Attempts);
        activity?.SetTag("ptm.correlation_id", envelope.CorrelationId.ToString("N"));

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

        var tenantInitializer = itemScope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>();

        if (job.TrainerId is null)
        {
            // Sem tenant só executa um handler de plataforma, numa allowlist fechada
            // verificada por testes de arquitetura. Nunca há contexto administrativo.
            if (handlers[0] is not IPlatformDurableJobHandler)
            {
                _logger.LogWarning(
                    JobDispatchLogEvents.TenantRejected,
                    "Durable job without tenant was rejected before handler execution.");
                return DispatchItemOutcome.PermanentFailure(TenantUnavailableCode);
            }

            tenantInitializer.Establish(
            null,
            userId: null,
            role: null,
            TenantOrigin.System,
            isAdministrative: false);
        }
        else
        {
            var tenantValidator = itemScope.ServiceProvider.GetRequiredService<JobTenantValidator>();
            // Os jobs de tenant são emails ao personal trainer e aos clientes, que continuam a usar
            // a app com a subscrição inativa, suspensa ou cancelada.
            if (!await tenantValidator.IsAvailableAsync(
                    job.TrainerId,
                    activationCancellationToken,
                    requireActiveSubscription: false))
            {
                _logger.LogWarning(
                    JobDispatchLogEvents.TenantRejected,
                    "Durable job tenant was rejected before handler execution.");
                return DispatchItemOutcome.PermanentFailure(TenantUnavailableCode);
            }

            tenantInitializer.Establish(
                job.TrainerId,
                userId: null,
                role: null,
                TenantOrigin.Job,
                isAdministrative: false);
        }

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
            RecordOutcome(job.JobType, "lease_lost");
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
            {
                _logger.LogInformation(
                    JobDispatchLogEvents.ItemSucceeded,
                    "Durable job completed successfully.");
                RecordOutcome(job.JobType, "succeeded");
            }
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
                RecordOutcome(job.JobType, "retry");
            }
            else if (updated)
            {
                _logger.LogError(
                    JobDispatchLogEvents.ItemDeadLettered,
                    "Durable job moved to dead letter with failure code {FailureCode}.",
                    outcome.FailureCode);
                RecordOutcome(job.JobType, "dead_letter");
            }
        }

        if (!updated)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.LeaseLost,
                "Durable job final transition was rejected because its lease was lost.");
            RecordOutcome(job.JobType, "lease_lost");
        }
    }

    // O tipo de job é uma constante do código: cardinalidade fechada, sem dados pessoais.
    private static void RecordOutcome(string itemType, string outcome)
    {
        PtManagerTelemetry.DispatchOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("ptm.queue", Queue),
            new KeyValuePair<string, object?>("ptm.item.type", itemType),
            new KeyValuePair<string, object?>("ptm.outcome", outcome));

        if (outcome is "retry" or "dead_letter")
            Activity.Current?.SetStatus(ActivityStatusCode.Error, outcome);
    }

    private async Task RecordBacklogAsync(CancellationToken cancellationToken)
    {
        // Sem listener (telemetria desligada) não se gasta uma query por ativação.
        if (!PtManagerTelemetry.DispatchBacklog.Enabled)
            return;

        // A contagem só serve a métrica: o trabalho da ativação já está feito e uma falha aqui
        // não pode transformá-la num erro (nem num evento Sentry).
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IDurableJobStore>();
            var due = await store.CountDueAsync(cancellationToken);
            PtManagerTelemetry.DispatchBacklog.Record(
                due, new KeyValuePair<string, object?>("ptm.queue", Queue));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.ActivationFailure,
                "Durable job backlog metric was skipped with failure type {FailureType}.",
                exception.GetType().Name);
        }
    }
}
