namespace Application.Features.Billing.Abstractions;

/// <summary>Resultados fechados da reserva local de Checkout.</summary>
public enum CheckoutReservationStatus
{
    Acquired,
    ResumeCreated,
    SubscriptionNotFound,
    SameKeyDifferentTier,
    AnotherOperationActive,
    AlreadySubscribed,
    BillingExempt,
    ConcurrencyConflict
}
