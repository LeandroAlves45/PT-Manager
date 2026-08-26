using Domain.ValueObjects;

namespace Application.Features.Billing.Webhooks;

/// <summary>Converte estados externos normalizados no estado local.</summary>
public static class SubscriptionStatusMapper
{
    public static SubscriptionStatus Map(
        string providerStatus
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerStatus);

        return providerStatus.Trim().ToLowerInvariant() switch
        {
            "trialing" or "active" => SubscriptionStatus.Active,
            "incomplete" or "incomplete_expired" => SubscriptionStatus.Inactive,
            "past_due" or "unpaid" or "paused" => SubscriptionStatus.Suspended,
            "canceled" => SubscriptionStatus.Cancelled,
            _ => SubscriptionStatus.Inactive
        };
    }
}
