using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Domain.Entities.Billing;
using Domain.Exceptions;

namespace Infrastructure.Persistence.Billing;

/// <summary>Aplica apenas transições suportadas por dados confiáveis.</summary>
internal static class BillingEventMapper
{
    internal static BillingEventApplyStatus Apply(
        TrainerSubscription subscription,
        NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? snapshot,
        DateTime now
    )
    {
        if (snapshot is not null)
        {
            if (HasExternalIdentityConflict(subscription, snapshot))
                return BillingEventApplyStatus.ExternalIdentityConflict;

            try
            {
                var applied = subscription.ApplyProviderSnapshot(
                    snapshot.ProviderCustomerId,
                    snapshot.ProviderSubscriptionId,
                    snapshot.Tier,
                    snapshot.ClientLimit,
                    SubscriptionStatusMapper.Map(snapshot.ProviderStatus),
                    snapshot.TrialEndsAt,
                    snapshot.ObservedAt,
                    now
                );

                return applied
                    ? BillingEventApplyStatus.Applied
                    : BillingEventApplyStatus.StaleSnapshot;
            }
            catch (DomainException)
            {
                return BillingEventApplyStatus.ReconciliationRequired;
            }
        }

        return paymentEvent.Kind == PaymentEventKind.TrialWillEnd
            ? BillingEventApplyStatus.NoStateChange
            : BillingEventApplyStatus.ReconciliationRequired;
    }

    private static bool HasExternalIdentityConflict(
        TrainerSubscription subscription,
        ProviderSubscriptionSnapshot snapshot)
    {
        var customerId = snapshot.ProviderCustomerId.Trim();
        var subscriptionId = snapshot.ProviderSubscriptionId.Trim();

        if (subscription.StripeCustomerId is not null &&
            subscription.StripeCustomerId != customerId)
        {
            return true;
        }

        var mayReplaceSubscription = subscription.Status == Domain.ValueObjects.SubscriptionStatus.Inactive ||
            subscription.Status == Domain.ValueObjects.SubscriptionStatus.Cancelled;

        return subscription.StripeSubscriptionId is not null &&
            subscription.StripeSubscriptionId != subscriptionId &&
            !mayReplaceSubscription;
    }
}
