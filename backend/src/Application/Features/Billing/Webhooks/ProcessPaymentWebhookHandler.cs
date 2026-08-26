using Application.Common.Abstractions;
using Application.Features.Billing.Abstractions;
using Application.Results;

namespace Application.Features.Billing.Webhooks;

/// <summary>Processa apenas eventos já autenticados e normalizados.</summary>
public sealed class ProcessPaymentWebhookHandler
{
    private readonly ISubscriptionReconciliationGateway _gateway;
    private readonly IPaymentEventStore _store;
    private readonly IClock _clock;

    public ProcessPaymentWebhookHandler(
        ISubscriptionReconciliationGateway gateway,
        IPaymentEventStore store,
        IClock clock)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        NormalizedPaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paymentEvent);

        if (paymentEvent.Kind == PaymentEventKind.Unknown)
            return Result.Success();
        ProviderSubscriptionSnapshot? snapshot = null;
        if (paymentEvent.ProviderCustomerId is not null ||
            paymentEvent.ProviderSubscriptionId is not null)
            snapshot = await _gateway.GetSubscriptionSnapshotAsync(
                paymentEvent.ProviderCustomerId,
                paymentEvent.ProviderSubscriptionId,
                cancellationToken
            );

        var committed = await _store.CommitAsync(
            paymentEvent,
            snapshot,
            _clock.UtcNow,
            cancellationToken
        );

        return committed.Kind switch
        {
            CommitPaymentEventStoreStatus.Processed or
            CommitPaymentEventStoreStatus.AlreadyProcessed => Result.Success(),
            CommitPaymentEventStoreStatus.SubscriptionNotFound =>
                Result.Failure(BillingErrors.SubscriptionNotFound),
            CommitPaymentEventStoreStatus.ExternalIdentityConflict =>
                Result.Failure(BillingErrors.ExternalIdentityConflict),
            CommitPaymentEventStoreStatus.ReconciliationRequired =>
                Result.Failure(BillingErrors.ReconciliationRequired),
            CommitPaymentEventStoreStatus.ConcurrencyConflict =>
                Result.Failure(BillingErrors.ConcurrencyConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(committed.Kind))
        };
    }
}
