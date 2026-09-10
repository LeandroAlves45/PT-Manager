using Application.Features.Billing.Webhooks;

namespace Application.Features.Billing.Abstractions;

/// <summary>Fronteira transacional do processamento local de eventos.</summary>
public interface IPaymentEventStore
{
    /// <summary>Evita uma chamada remota quando o evento já foi confirmado.</summary>
    Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken);

    Task<CommitPaymentEventStoreResult> CommitAsync(
        NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? reconciledSnapshot,
        DateTime now,
        CancellationToken cancellationToken
    );
}
