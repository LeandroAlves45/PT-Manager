namespace Application.Features.Billing.Abstractions;

/// <summary>
/// Resultado da leitura do pagamento de uma fatura. Sucesso sem <c>Payment</c> significa que a
/// fatura não movimentou dinheiro pelo Stripe (valor zero, por exemplo no trial).
/// </summary>
public sealed record InvoicePaymentOutcome(
    SubscriptionReconciliationStatus Status,
    InvoicePaymentSnapshot? Payment = null);
