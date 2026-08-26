namespace Application.Features.Billing.Abstractions;

/// <summary>Resultados esperados ao associar o primeiro customer.</summary>
public enum LinkPaymentCustomerStoreStatus
{
    Linked,
    AlreadyLinkedToSameCustomer,
    LinkedToDifferentCustomer,
    SubscriptionNotFound,
    ConcurrencyConflict
}
