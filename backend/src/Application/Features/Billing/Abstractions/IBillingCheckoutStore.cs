using Domain.ValueObjects;

namespace Application.Features.Billing.Abstractions;

/// <summary>Persistência necessária a Checkout e Customer Portal.</summary>
public interface IBillingCheckoutStore
{
    Task<CheckoutReservationResult> ReserveAsync(
        Guid trainerId,
        Guid clientOperationId,
        SubscriptionTier tier,
        DateTime now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken
    );

    Task<CheckoutMutationStatus> LinkCustomerAsync(
        Guid operationId,
        Guid leaseOwnerId,
        string providerCustomerId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<CheckoutMutationStatus> MarkSessionCreatedAsync(
        Guid operationId,
        Guid leaseOwnerId,
        string providerSessionId,
        DateTime sessionExpiresAt,
        DateTime now,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid operationId,
        Guid leaseOwnerId,
        string failureCode,
        DateTime now,
        CancellationToken cancellationToken);
    Task<string?> GetCustomerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    );
}
