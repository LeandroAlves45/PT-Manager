using Domain.ValueObjects;

namespace Application.Features.Billing.Abstractions;

/// <summary>Estado atual obtido através de configuração confiável.</summary>
public sealed record ProviderSubscriptionSnapshot(
    string ProviderCustomerId,
    string ProviderSubscriptionId,
    SubscriptionTier Tier,
    string ProviderStatus,
    DateTime? TrialEndsAt,
    DateTime ObservedAt
);
