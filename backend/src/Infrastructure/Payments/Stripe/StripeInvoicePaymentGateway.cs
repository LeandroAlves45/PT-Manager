using Application.Features.Billing.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.Payments.Stripe;

/// <summary>
/// Soma os pagamentos de uma fatura a partir das balance transactions, a única fonte do valor
/// líquido. Uma balance transaction ainda por criar é falha transitória: o webhook responde
/// com erro e o Stripe volta a entregar o evento.
/// </summary>
internal sealed class StripeInvoicePaymentGateway : IInvoicePaymentGateway
{
    private readonly StripeClientFactory _clientFactory;
    private readonly StripeOptions _options;

    public StripeInvoicePaymentGateway(
        StripeClientFactory clientFactory,
        IOptions<StripeOptions> options)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<InvoicePaymentOutcome> GetPaidInvoiceAsync(
        string providerInvoiceId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(SubscriptionReconciliationStatus.Disabled);

        try
        {
            var service = new InvoicePaymentService(_clientFactory.GetRequiredClient());
            var payments = await service.ListAsync(
                new InvoicePaymentListOptions
                {
                    Invoice = providerInvoiceId,
                    Status = "paid",
                    Limit = 100,
                    Expand =
                    [
                        "data.payment.payment_intent.latest_charge.balance_transaction",
                        "data.payment.charge.balance_transaction"
                    ]
                },
                cancellationToken: cancellationToken);

            long amount = 0;
            long net = 0;
            string? currency = null;
            DateTime? paidAt = null;

            foreach (var payment in payments.Data)
            {
                var charge = payment.Payment?.Type switch
                {
                    "payment_intent" => payment.Payment.PaymentIntent?.LatestCharge,
                    "charge" => payment.Payment.Charge,
                    _ => null
                };
                // Pagamentos fora do Stripe não têm balance transaction nem dinheiro recebido aqui.
                if (charge is null)
                    continue;

                var transaction = charge.BalanceTransaction;
                if (transaction is null)
                    return new(SubscriptionReconciliationStatus.TransientFailure);
                if (currency is not null && currency != transaction.Currency)
                    return new(SubscriptionReconciliationStatus.ConfigurationMismatch);

                currency = transaction.Currency;
                amount += transaction.Amount;
                net += transaction.Net;
                var paymentPaidAt = payment.StatusTransitions?.PaidAt ?? payment.Created;
                if (paidAt is null || paymentPaidAt > paidAt)
                    paidAt = paymentPaidAt;
            }

            if (amount <= 0 || currency is null || paidAt is null)
                return new(SubscriptionReconciliationStatus.Success);

            return new(
                SubscriptionReconciliationStatus.Success,
                new InvoicePaymentSnapshot(
                    providerInvoiceId,
                    amount,
                    net,
                    currency,
                    DateTime.SpecifyKind(paidAt.Value, DateTimeKind.Utc)));
        }
        catch (StripeException exception)
        {
            return new(StripeFailureMapper.Map(exception) switch
            {
                BillingGatewayStatus.TransientFailure => SubscriptionReconciliationStatus.TransientFailure,
                BillingGatewayStatus.NotFound => SubscriptionReconciliationStatus.NotFound,
                _ => SubscriptionReconciliationStatus.InvalidResponse
            });
        }
    }
}
