namespace Application.Features.Billing.Abstractions;

/// <summary>
/// Dinheiro que a plataforma recebeu numa fatura paga, na moeda de liquidação do Stripe.
/// <see cref="NetCents"/> já exclui a comissão do Stripe (RN-018).
/// </summary>
public sealed record InvoicePaymentSnapshot(
    string ProviderInvoiceId,
    long AmountPaidCents,
    long NetCents,
    string Currency,
    DateTime PaidAt
);
