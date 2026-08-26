namespace Application.Features.Billing.Webhooks;

/// <summary>Tipos autenticados suportados pelo core.</summary>
public enum PaymentEventKind
{
    Unknown,
    CheckoutCompleted,
    SubscriptionUpdated,
    SubscriptionDeleted,
    InvoicePaymentSucceeded,
    InvoicePaymentFailed,
    TrialWillEnd
}
