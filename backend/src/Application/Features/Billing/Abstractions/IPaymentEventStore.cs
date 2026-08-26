using Application.Features.Billing.Webhooks;

namespace Application.Features.Billing.Abstractions;

/// <summary>Fronteira transacional do processamento local de eventos.</summary>
public interface IPaymentEventStore
{
    Task<CommitPaymentEventStoreResult> CommitAsync(
        NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? reconciledSnapshot,
        DateTime now,
        CancellationToken cancellationToken
    );
}
