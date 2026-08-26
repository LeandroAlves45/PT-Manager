using Domain.ValueObjects;

namespace Application.Features.Billing.Abstractions;

/// <summary>Pedido provider-neutral para criar Checkout.</summary>
public sealed record CreateCheckoutRequest(
    Guid TrainerId,
    Guid OperationId,
    string? ProviderCustomerId,
    string TrainerEmail,
    SubscriptionTier Tier,
    Uri SuccessUrl,
    Uri CancelUrl,
    string IdempotencyKey
);
