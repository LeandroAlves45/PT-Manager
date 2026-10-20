using Domain.Exceptions;

namespace Domain.Entities.Billing;

/// <summary>
/// Pagamento confirmado de uma fatura de subscrição da plataforma (RN-018). Guarda o valor
/// líquido, já sem a comissão do Stripe, porque é esse o dinheiro que a plataforma recebe.
/// A unicidade de <see cref="StripeInvoiceId"/> impede contar a mesma fatura duas vezes.
/// </summary>
public sealed class PlatformSubscriptionPayment
{
    public Guid Id { get; private set; }
    public Guid TrainerId { get; private set; }
    public string StripeInvoiceId { get; private set; } = null!;
    public long AmountPaidCents { get; private set; }
    public long NetCents { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTime PaidAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PlatformSubscriptionPayment() { }

    /// <summary>Regista um pagamento com valor positivo.</summary>
    public PlatformSubscriptionPayment(
        Guid trainerId,
        string stripeInvoiceId,
        long amountPaidCents,
        long netCents,
        string currency,
        DateTime paidAt,
        DateTime now
    )
    {
        var normalizedInvoiceId = stripeInvoiceId?.Trim() ?? string.Empty;
        var normalizedCurrency = currency?.Trim().ToLowerInvariant() ?? string.Empty;

        if (trainerId == Guid.Empty)
            throw new DomainException("Trainer ID is required.");
        if (normalizedInvoiceId.Length is 0 or > 255)
            throw new DomainException("Stripe invoice ID must contain between 1 and 255 characters.");
        if (amountPaidCents <= 0)
            throw new DomainException("A platform payment must have a positive amount.");
        if (netCents > amountPaidCents)
            throw new DomainException("The net amount cannot exceed the amount paid.");
        if (normalizedCurrency.Length != 3)
            throw new DomainException("Currency must be a three-letter ISO code.");
        if (paidAt.Kind != DateTimeKind.Utc)
            throw new DomainException("Payment time must be UTC.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        StripeInvoiceId = normalizedInvoiceId;
        AmountPaidCents = amountPaidCents;
        NetCents = netCents;
        Currency = normalizedCurrency;
        PaidAt = paidAt;
        CreatedAt = now;
    }
}
