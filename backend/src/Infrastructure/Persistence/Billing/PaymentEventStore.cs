using System.Text.Json;
using Application.Common.Abstractions;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Domain.Entities.Billing;
using Domain.Entities.Jobs;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Billing;

/// <summary>Confirma subscription, deduplicado e outbox numa transação.</summary>
internal sealed class PaymentEventStore : IPaymentEventStore
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions =
        new(JsonSerializerDefaults.General);

    private readonly PtManagerDbContext _dbContext;
    private readonly ITenantContextInitializer _tenantInitializer;

    public PaymentEventStore(
        PtManagerDbContext dbContext,
        ITenantContextInitializer tenantInitializer
    )
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantInitializer = tenantInitializer ?? throw new ArgumentNullException(nameof(tenantInitializer));
    }

    public async Task<CommitPaymentEventStoreResult> CommitAsync(
        NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? snapshot,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        if (await IsAlreadyProcessedAsync(paymentEvent.EventId, cancellationToken))
            return new(CommitPaymentEventStoreStatus.AlreadyProcessed);

        // A resolução do trainer usa apenas identidades externas já persistidas;
        // metadata do provider nunca concede autorização.
        var customerId = snapshot?.ProviderCustomerId.Trim() ??
            paymentEvent.ProviderCustomerId?.Trim();
        var subscriptionId = snapshot?.ProviderSubscriptionId.Trim() ??
            paymentEvent.ProviderSubscriptionId?.Trim();
        var matches = await _dbContext.TrainerSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(sub => (customerId != null && sub.StripeCustomerId == customerId) ||
                (subscriptionId != null && sub.StripeSubscriptionId == subscriptionId))
            .Select(sub => sub.TrainerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
            return new(CommitPaymentEventStoreStatus.SubscriptionNotFound);
        if (matches.Count != 1)
            return new(CommitPaymentEventStoreStatus.ExternalIdentityConflict);

        var trainerId = matches[0];

        // O tenant é estabelecido uma única vez antes da operação repetível;
        // o delegate de retry não volta a estabelecer o tenant.
        _tenantInitializer.Establish(trainerId, null, null, TenantOrigin.Webhook, false);

        var status = CommitPaymentEventStoreStatus.Processed;
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteInTransactionAsync(
                async operationToken =>
                {
                    // O delegate pode ser reexecutado pela strategy; limpar o
                    // tracker garante que cada tentativa parte de estado limpo.
                    _dbContext.ChangeTracker.Clear();
                    status = await StageEventEffectsAsync(
                        paymentEvent,
                        snapshot,
                        now,
                        trainerId,
                        operationToken);

                    if (status == CommitPaymentEventStoreStatus.Processed)
                    {
                        await _dbContext.SaveChangesAsync(
                            acceptAllChangesOnSuccess: false,
                            operationToken);
                    }
                },
                verifyToken => IsAlreadyProcessedAsync(paymentEvent.EventId, verifyToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(CommitPaymentEventStoreStatus.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgres &&
            postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName == "uq_processed_stripe_events_event_id")
        {
            return new(CommitPaymentEventStoreStatus.AlreadyProcessed);
        }

        _dbContext.ChangeTracker.Clear();
        return new(status);
    }

    private async Task<CommitPaymentEventStoreStatus> StageEventEffectsAsync(
        NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? snapshot,
        DateTime now,
        Guid trainerId,
        CancellationToken cancellationToken
    )
    {
        var subscription = await _dbContext.TrainerSubscriptions
            .FromSqlInterpolated(
                $"SELECT * FROM trainer_subscriptions WHERE trainer_id = {trainerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (subscription is null)
            return CommitPaymentEventStoreStatus.SubscriptionNotFound;

        var applyStatus = BillingEventMapper.Apply(subscription, paymentEvent, snapshot, now);
        if (applyStatus == BillingEventApplyStatus.ExternalIdentityConflict)
            return CommitPaymentEventStoreStatus.ExternalIdentityConflict;
        if (applyStatus == BillingEventApplyStatus.ReconciliationRequired)
            return CommitPaymentEventStoreStatus.ReconciliationRequired;

        // Applied, StaleSnapshot e NoStateChange terminam deduplicados; um
        // snapshot obsoleto não altera a subscrição nem cria outbox.
        _dbContext.ProcessedStripeEvents.Add(new ProcessedStripeEvent(
            paymentEvent.EventId,
            paymentEvent.EventType,
            now
        ));

        if (applyStatus == BillingEventApplyStatus.Applied &&
            paymentEvent.Kind is PaymentEventKind.InvoicePaymentFailed or
                PaymentEventKind.TrialWillEnd)
        {
            var recipientEmail = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == trainerId && user.Role == "trainer" &&
                    user.IsActive && !user.IsDeleted)
                .Select(user => user.Email)
                .SingleOrDefaultAsync(cancellationToken);

            // Sem destinatário ativo, a alteração autoritativa e o deduplicado
            // são confirmados sem notificação: evita retries permanentes.
            if (recipientEmail is not null)
            {
                var payload = JsonSerializer.Serialize(new BillingNotificationPayload(
                    trainerId,
                    recipientEmail,
                    paymentEvent.EventId,
                    paymentEvent.Kind.ToString()
                ), PayloadSerializerOptions);
                _dbContext.OutboxMessages.Add(new OutboxMessage(
                    trainerId,
                    "billing_notification",
                    payload,
                    $"billing:{paymentEvent.EventId}:{paymentEvent.Kind}",
                    paymentEvent.CorrelationId,
                    now
                ));
            }
        }

        return CommitPaymentEventStoreStatus.Processed;
    }

    private Task<bool> IsAlreadyProcessedAsync(
        string eventId,
        CancellationToken cancellationToken
    ) => _dbContext.ProcessedStripeEvents
        .AsNoTracking()
        .AnyAsync(processed => processed.StripeEventId == eventId, cancellationToken);

    /// <summary>Contrato snake_case consumido pelo dispatcher de notificações.</summary>
    private sealed record BillingNotificationPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("trainer_id")] Guid TrainerId,
        [property: System.Text.Json.Serialization.JsonPropertyName("recipient_email")] string RecipientEmail,
        [property: System.Text.Json.Serialization.JsonPropertyName("event_id")] string EventId,
        [property: System.Text.Json.Serialization.JsonPropertyName("kind")] string Kind
    );
}
