using Application.Common.Abstractions;
using Application.Common.Observability;
using Application.Features.Billing.Abstractions;
using Application.Errors;
using Application.Results;

namespace Application.Features.Billing.Webhooks;

/// <summary>Processa apenas eventos já autenticados e normalizados.</summary>
public sealed class ProcessPaymentWebhookHandler
{
    private readonly ISubscriptionReconciliationGateway _gateway;
    private readonly IInvoicePaymentGateway _invoiceGateway;
    private readonly IPaymentEventStore _store;
    private readonly IClock _clock;

    public ProcessPaymentWebhookHandler(
        ISubscriptionReconciliationGateway gateway,
        IInvoicePaymentGateway invoiceGateway,
        IPaymentEventStore store,
        IClock clock)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _invoiceGateway = invoiceGateway ?? throw new ArgumentNullException(nameof(invoiceGateway));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        NormalizedPaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paymentEvent);

        if (paymentEvent.Kind == PaymentEventKind.Unknown)
            return Recorded(Result.Success(), "ignored");

        if (await _store.IsProcessedAsync(paymentEvent.EventId, cancellationToken))
            return Recorded(Result.Success(), "duplicate");

        ProviderSubscriptionSnapshot? snapshot = null;
        if (paymentEvent.ProviderCustomerId is not null ||
            paymentEvent.ProviderSubscriptionId is not null)
        {
            var reconciliation = await _gateway.GetSubscriptionSnapshotAsync(
                paymentEvent.ProviderCustomerId,
                paymentEvent.ProviderSubscriptionId,
                cancellationToken
            );

            if (reconciliation.Status != SubscriptionReconciliationStatus.Success)
                return Recorded(Result.Failure(MapReconciliationError(reconciliation.Status)), "failed");

            snapshot = reconciliation.Snapshot;
        }

        // O painel soma o líquido das faturas pagas; sem esse valor o evento não fecha,
        // para o Stripe voltar a entregá-lo em vez de o dinheiro ficar por contar.
        InvoicePaymentSnapshot? invoicePayment = null;
        if (paymentEvent.Kind == PaymentEventKind.InvoicePaid &&
            paymentEvent.ProviderInvoiceId is not null)
        {
            var paid = await _invoiceGateway.GetPaidInvoiceAsync(
                paymentEvent.ProviderInvoiceId,
                cancellationToken
            );

            if (paid.Status != SubscriptionReconciliationStatus.Success)
                return Recorded(Result.Failure(MapReconciliationError(paid.Status)), "failed");

            invoicePayment = paid.Payment;
        }

        var committed = await _store.CommitAsync(
            paymentEvent,
            snapshot,
            invoicePayment,
            _clock.UtcNow,
            cancellationToken
        );

        return committed.Kind switch
        {
            CommitPaymentEventStoreStatus.Processed => Recorded(Result.Success(), "processed"),
            // Outra entrega do mesmo evento ganhou a corrida entre o IsProcessed e o commit.
            CommitPaymentEventStoreStatus.AlreadyProcessed => Recorded(Result.Success(), "duplicate"),
            CommitPaymentEventStoreStatus.SubscriptionNotFound =>
                Recorded(Result.Failure(BillingErrors.SubscriptionNotFound), "failed"),
            CommitPaymentEventStoreStatus.ExternalIdentityConflict =>
                Recorded(Result.Failure(BillingErrors.ExternalIdentityConflict), "failed"),
            CommitPaymentEventStoreStatus.ReconciliationRequired =>
                Recorded(Result.Failure(BillingErrors.ReconciliationRequired), "failed"),
            CommitPaymentEventStoreStatus.ConcurrencyConflict =>
                Recorded(Result.Failure(BillingErrors.ConcurrencyConflict), "failed"),
            _ => throw new ArgumentOutOfRangeException(nameof(committed.Kind))
        };
    }

    // Conta o desfecho sem o tipo nem o id do evento: o alerta só precisa de saber que
    // um webhook falhou, e o id ficaria uma série nova por evento.
    private static Result Recorded(Result result, string outcome)
    {
        PtManagerTelemetry.StripeWebhooks.Add(
            1,
            new KeyValuePair<string, object?>("ptm.outcome", outcome));
        return result;
    }

    private static Error MapReconciliationError(SubscriptionReconciliationStatus status) =>
        status switch
        {
            SubscriptionReconciliationStatus.Disabled => BillingErrors.StripeDisabled,
            SubscriptionReconciliationStatus.TransientFailure => BillingErrors.ProviderUnavailable,
            SubscriptionReconciliationStatus.NotFound => BillingErrors.SubscriptionNotFound,
            SubscriptionReconciliationStatus.InvalidResponse => BillingErrors.ProviderInvalidResponse,
            SubscriptionReconciliationStatus.ConfigurationMismatch =>
                BillingErrors.ProviderConfigurationMismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}
