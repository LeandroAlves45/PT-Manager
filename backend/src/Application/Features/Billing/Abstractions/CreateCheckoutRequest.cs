using Domain.ValueObjects;

namespace Application.Features.Billing.Abstractions;

/// <summary>Pedido provider-neutral para criar uma Checkout Session.</summary>
public sealed record CreateCheckoutRequest(
    Guid TrainerId,
    Guid OperationId,
    string ProviderCustomerId,
    SubscriptionTier Tier,
    DateTime? EffectiveTrialEndsAt,
    string IdempotencyKey
);
