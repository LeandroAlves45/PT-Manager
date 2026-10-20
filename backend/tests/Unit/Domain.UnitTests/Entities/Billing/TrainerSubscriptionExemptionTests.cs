using Domain.Entities.Billing;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Billing;

/// <summary>Isenção de faturação pelo superuser (RN-008, U42, U43).</summary>
public sealed class TrainerSubscriptionExemptionTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Exempting_MovesToActiveProWithoutLimitOrTrial()
    {
        var subscription = Suspended();

        subscription.SetBillingExemption(true, Now.AddHours(1));

        Assert.True(subscription.IsExemptFromBilling);
        Assert.Equal(SubscriptionTier.Pro, subscription.Tier);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.ClientLimit);
        Assert.Null(subscription.TrialEndsAt);
        Assert.Equal(Now.AddHours(1), subscription.BillingExemptSince);
    }

    [Fact]
    public void ExemptingTwice_KeepsTheOriginalStartDate()
    {
        var subscription = Suspended();
        subscription.SetBillingExemption(true, Now.AddHours(1));

        subscription.SetBillingExemption(true, Now.AddHours(5));

        Assert.Equal(Now.AddHours(1), subscription.BillingExemptSince);
    }

    [Fact]
    public void RemovingTheExemption_LeavesAnInactiveFreeAccountThatCannotAddClients()
    {
        var subscription = Suspended();
        subscription.SetBillingExemption(true, Now.AddHours(1));

        subscription.SetBillingExemption(false, Now.AddHours(2));

        Assert.False(subscription.IsExemptFromBilling);
        Assert.Null(subscription.BillingExemptSince);
        Assert.Equal(SubscriptionTier.Free, subscription.Tier);
        Assert.Equal(SubscriptionStatus.Inactive, subscription.Status);
        Assert.Null(subscription.TrialEndsAt);
        Assert.False(subscription.CanAddClient());
    }

    [Fact]
    public void RemovingWhenNotExempt_ChangesNothing()
    {
        var subscription = Suspended();

        subscription.SetBillingExemption(false, Now.AddHours(2));

        Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(Now, subscription.UpdatedAt);
    }

    [Fact]
    public void WhileExempt_ALaterStripeSnapshotDoesNotDowngrade()
    {
        var subscription = Suspended();
        subscription.SetBillingExemption(true, Now.AddHours(1));

        var applied = subscription.ApplyProviderSnapshot(
            "cus_1",
            "sub_1",
            SubscriptionTier.Starter,
            SubscriptionStatus.Cancelled,
            null,
            Now.AddHours(2),
            Now.AddHours(2));

        Assert.False(applied);
        Assert.Equal(SubscriptionTier.Pro, subscription.Tier);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public void AfterTheExemptionEnds_AStripeSnapshotAppliesAgain()
    {
        var subscription = Suspended();
        subscription.SetBillingExemption(true, Now.AddHours(1));
        subscription.SetBillingExemption(false, Now.AddHours(2));

        var applied = subscription.ApplyProviderSnapshot(
            "cus_1",
            "sub_2",
            SubscriptionTier.Starter,
            SubscriptionStatus.Active,
            null,
            Now.AddHours(3),
            Now.AddHours(3));

        Assert.True(applied);
        Assert.Equal(SubscriptionTier.Starter, subscription.Tier);
    }

    private static TrainerSubscription Suspended()
    {
        var subscription = new TrainerSubscription(Guid.NewGuid(), Now.AddDays(7), Now);
        subscription.Suspend(Now);
        return subscription;
    }
}
