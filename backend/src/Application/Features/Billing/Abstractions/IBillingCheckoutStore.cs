namespace Application.Features.Billing.Abstractions;

/// <summary>Persistência necessária a Checkout e Customer Portal.</summary>
public interface IBillingCheckoutStore
{
    Task<CheckoutContext?> GetCheckoutContextAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    );

    Task<LinkPaymentCustomerStoreResult> LinkCustomerAsync(
        Guid trainerId,
        string providerCustomerId,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<string?> GetCustomerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    );
}
