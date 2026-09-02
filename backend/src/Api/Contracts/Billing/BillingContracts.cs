using Application.Features.Billing.Dtos;

namespace Api.Contracts.Billing;

/// <summary>Estado da subscrição do personal trainer autenticado.</summary>
public sealed record SubscriptionResponse(
    string Status,
    string Tier,
    int ClientLimit,
    int CurrentClientCount,
    DateTime? TrialEndsAt)
{
    /// <summary>Projeta a subscrição da Application.</summary>
    public static SubscriptionResponse From(SubscriptionDto subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new(
            subscription.Status,
            subscription.Tier,
            subscription.ClientLimit,
            subscription.CurrentClientCount,
            subscription.TrialEndsAt
        );
    }
}
