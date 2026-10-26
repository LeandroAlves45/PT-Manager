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

/// <summary>Processa uma passagem limitada pelas mensagens de outbox pendentes.</summary>
internal sealed class OutboxDispatcher
{
    private const string UnexpectedFailureCode = "outbox_handler_unexpected_failure";
    private const string TenantUnavailableCode = "outbox_tenant_unavailable";
    private const string HandlerMissingCode = "outbox_handler_not_registered";
    private const string Queue = "outbox";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobDispatchOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<JobDispatchOptions> options,
        IClock clock,
        ILogger<OutboxDispatcher> logger)
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
        var allowedTypes = ResolveRegisteredMessageTypes();
        if (allowedTypes.Count == 0)
            return;

        var remaining = _options.OutboxBatchSize;

        while (remaining > 0 && !cancellationToken.IsCancellationRequested)
        {
            var claimSize = Math.Min(remaining, _options.MaxConcurrencyPerDispatcher);
            var claimed = await ClaimAsync(claimSize, allowedTypes, cancellationToken);
            if (claimed.Count == 0)
                return;

            remaining -= claimed.Count;
            await Task.WhenAll(claimed.Select(message =>
                ProcessAsync(message, cancellationToken)));
        }
    }

    private IReadOnlyCollection<string> ResolveRegisteredMessageTypes()
    {
        using var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider
            .GetServices<IOutboxMessageHandler>()
            .Select(handler => handler.MessageType)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        int claimSize,
        IReadOnlyCollection<string> allowedMessageTypes,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        return await store.ClaimPendingAsync(
            _options.LeaseDuration,
            claimSize,
            _options.MaxAttempts,
            cancellationToken,
            allowedMessageTypes);
    }

    private async Task ProcessAsync(
        OutboxMessage claimed,
        CancellationToken activationCancellationToken)
    {
        if (!claimed.LeaseOwnerId.HasValue)
            throw new InvalidOperationException("A claimed outbox message must have a lease owner.");

        var envelope = new OutboxMessageEnvelope(
            claimed.Id,
            claimed.TrainerId,
            claimed.MessageType,
            claimed.Payload,
            claimed.IdempotencyKey,
            claimed.CorrelationId,
            claimed.Attempts,
            claimed.LeaseOwnerId.Value);

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OutboxMessageId"] = envelope.Id,
            ["MessageType"] = envelope.MessageType,
            ["MessageAttempt"] = envelope.Attempts,
            ["CorrelationId"] = envelope.CorrelationId,
            ["TrainerId"] = envelope.TrainerId
        });

        using var activity = PtManagerTelemetry.ActivitySource.StartActivity(
            "outbox_message process",
            ActivityKind.Consumer);
        activity?.SetTag("ptm.queue", Queue);
        activity?.SetTag("ptm.item.type", envelope.MessageType);
        activity?.SetTag("ptm.item.attempt", envelope.Attempts);
        activity?.SetTag("ptm.correlation_id", envelope.CorrelationId.ToString("N"));

        DispatchItemOutcome outcome;
        try
        {
            outcome = await ExecuteHandlerAsync(envelope, activationCancellationToken);
        }
        catch (OperationCanceledException) when (activationCancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                JobDispatchLogEvents.ActivationFailure,
                "Outbox handler failed unexpectedly with failure type {FailureType}.",
                exception.GetType().Name);
            outcome = DispatchItemOutcome.TransientFailure(UnexpectedFailureCode);
        }

        await ApplyOutcomeAsync(envelope, outcome, activationCancellationToken);
    }

    private async Task<DispatchItemOutcome> ExecuteHandlerAsync(
        OutboxMessageEnvelope message,
        CancellationToken activationCancellationToken)
    {
        await using var itemScope = _scopeFactory.CreateAsyncScope();
        var handlers = itemScope.ServiceProvider
            .GetServices<IOutboxMessageHandler>()
            .Where(handler => handler.MessageType == message.MessageType)
            .Take(2)
            .ToArray();

        if (handlers.Length == 0)
            return DispatchItemOutcome.PermanentFailure(HandlerMissingCode);

        if (handlers.Length > 1)
            throw new InvalidOperationException(
                "More than one outbox handler is registered for the same route.");

        var tenantValidator = itemScope.ServiceProvider.GetRequiredService<JobTenantValidator>();
        if (!await tenantValidator.IsAvailableAsync(
                message.TrainerId,
                activationCancellationToken,
                handlers[0].RequiresActiveSubscription))
        {
            _logger.LogWarning(
                JobDispatchLogEvents.TenantRejected,
                "Outbox tenant was rejected before handler execution.");
            return DispatchItemOutcome.PermanentFailure(TenantUnavailableCode);
        }

        var tenantInitializer = itemScope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>();
        tenantInitializer.Establish(
            message.TrainerId,
            userId: null,
            role: null,
            TenantOrigin.Job,
            isAdministrative: false);

        await using var heartbeat = LeaseHeartbeat.Start(
            token => RenewLeaseAsync(message.Id, message.LeaseOwnerId, token),
            _options.LeaseRenewalInterval,
            activationCancellationToken);

        DispatchItemOutcome outcome;
        try
        {
            outcome = await handlers[0].HandleAsync(message, heartbeat.ProcessingToken);
        }
        catch (OperationCanceledException) when (heartbeat.LeaseLost)
        {
            outcome = DispatchItemOutcome.LeaseLost();
        }

        await heartbeat.StopAsync();
        return heartbeat.LeaseLost ? DispatchItemOutcome.LeaseLost() : outcome;
    }

    private async Task<bool> RenewLeaseAsync(
        Guid messageId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        return await store.TryRenewLeaseAsync(
            messageId,
            leaseOwnerId,
            _options.LeaseDuration,
            cancellationToken);
    }

    private async Task ApplyOutcomeAsync(
        OutboxMessageEnvelope message,
        DispatchItemOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (outcome.Kind == DispatchItemOutcomeKind.LeaseLost)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.LeaseLost,
                "Outbox processing stopped because its lease was lost.");
            RecordOutcome(message.MessageType, "lease_lost");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        bool updated;
        if (outcome.Kind == DispatchItemOutcomeKind.Succeeded)
        {
            updated = await store.TryCompleteAsync(
                message.Id,
                message.LeaseOwnerId,
                cancellationToken);
            if (updated)
            {
                _logger.LogInformation(
                    JobDispatchLogEvents.ItemSucceeded,
                    "Outbox message completed successfully.");
                RecordOutcome(message.MessageType, "succeeded");
            }
        }
        else
        {
            var nextAttemptAt = outcome.Kind == DispatchItemOutcomeKind.TransientFailure
                ? RetryScheduleCalculator.CalculateNextAttempt(
                    _clock.UtcNow,
                    message.Attempts,
                    message.IdempotencyKey,
                    _options)
                : null;

            updated = await store.TryRecordFailureAsync(
                message.Id,
                message.LeaseOwnerId,
                outcome.FailureCode!,
                nextAttemptAt,
                cancellationToken);

            if (updated && nextAttemptAt.HasValue)
            {
                _logger.LogWarning(
                    JobDispatchLogEvents.ItemRetryScheduled,
                    "Outbox message scheduled for retry with failure code {FailureCode}.",
                    outcome.FailureCode);
                RecordOutcome(message.MessageType, "retry");
            }
            else if (updated)
            {
                _logger.LogError(
                    JobDispatchLogEvents.ItemDeadLettered,
                    "Outbox message moved to dead letter with failure code {FailureCode}.",
                    outcome.FailureCode);
                RecordOutcome(message.MessageType, "dead_letter");
            }
        }

        if (!updated)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.LeaseLost,
                "Outbox final transition was rejected because its lease was lost.");
            RecordOutcome(message.MessageType, "lease_lost");
        }
    }

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
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var due = await store.CountDueAsync(cancellationToken);
            PtManagerTelemetry.DispatchBacklog.Record(
                due, new KeyValuePair<string, object?>("ptm.queue", Queue));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                JobDispatchLogEvents.ActivationFailure,
                "Outbox backlog metric was skipped with failure type {FailureType}.",
                exception.GetType().Name);
        }
    }
}
