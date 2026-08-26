using Domain.Entities.Billing;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Billing;

public sealed class TrainerSubscriptionBillingTests
{
    private static readonly DateTime Now =
        new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LinkStripeCustomer_IdentifierWithOuterWhitespace_NormalizesIdentifier()
    {
        var subscription = Create();

        subscription.LinkStripeCustomer("  cus_1  ", Now.AddMinutes(1));

        Assert.Equal("cus_1", subscription.StripeCustomerId);
    }

    [Fact]
    public void LinkStripeCustomer_SameIdentifier_IsIdempotent()
    {
        var subscription = Create();
        var firstLinkAt = Now.AddMinutes(1);
        subscription.LinkStripeCustomer("cus_1", firstLinkAt);

        subscription.LinkStripeCustomer(" cus_1 ", Now.AddMinutes(2));

        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Equal(firstLinkAt, subscription.UpdatedAt);
    }

    [Fact]
    public void LinkStripeCustomer_DifferentIdentifier_ThrowsWithoutChangingExistingLink()
    {
        var subscription = Create();
        var firstLinkAt = Now.AddMinutes(1);
        subscription.LinkStripeCustomer("cus_1", firstLinkAt);

        Assert.Throws<DomainException>(() =>
            subscription.LinkStripeCustomer("cus_2", Now.AddMinutes(2)));
        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Equal(firstLinkAt, subscription.UpdatedAt);
    }

    [Fact]
    public void LinkStripeSubscription_IdentifiersWithOuterWhitespace_NormalizesBothIdentifiers()
    {
        var subscription = Create();

        subscription.LinkStripeSubscription(
            "  cus_1  ",
            "  sub_1  ",
            Now.AddMinutes(1)
        );

        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Equal("sub_1", subscription.StripeSubscriptionId);
    }

    [Fact]
    public void LinkStripeSubscription_SameIdentifiers_IsIdempotent()
    {
        var subscription = Create();
        var firstLinkAt = Now.AddMinutes(1);
        subscription.LinkStripeSubscription("cus_1", "sub_1", firstLinkAt);

        subscription.LinkStripeSubscription(
            " cus_1 ",
            " sub_1 ",
            Now.AddMinutes(2)
        );

        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Equal("sub_1", subscription.StripeSubscriptionId);
        Assert.Equal(firstLinkAt, subscription.UpdatedAt);
    }

    [Fact]
    public void LinkStripeSubscription_ActiveSubscriptionReplacement_ThrowsWithoutChangingLink()
    {
        var subscription = Create();
        var firstLinkAt = Now.AddMinutes(1);
        subscription.LinkStripeSubscription("cus_1", "sub_1", firstLinkAt);

        Assert.Throws<DomainException>(() => subscription.LinkStripeSubscription(
            "cus_1",
            "sub_2",
            Now.AddMinutes(2)
        ));
        Assert.Equal("sub_1", subscription.StripeSubscriptionId);
        Assert.Equal(firstLinkAt, subscription.UpdatedAt);
    }

    [Fact]
    public void LinkStripeSubscription_CancelledSubscriptionReplacement_ReplacesLink()
    {
        var subscription = Create();
        subscription.LinkStripeSubscription("cus_1", "sub_1", Now.AddMinutes(1));
        subscription.Cancel(Now.AddMinutes(2));
        var replacementAt = Now.AddMinutes(3);

        subscription.LinkStripeSubscription("cus_1", "sub_2", replacementAt);

        Assert.Equal("sub_2", subscription.StripeSubscriptionId);
        Assert.Equal(replacementAt, subscription.UpdatedAt);
    }

    [Fact]
    public void ApplyProviderSnapshot_NewerSnapshot_AppliesCompleteAuthoritativeState()
    {
        var subscription = Create();
        var observedAt = Now.AddMinutes(5);
        var appliedAt = Now.AddMinutes(6);
        var trialEndsAt = Now.AddDays(30);

        var applied = subscription.ApplyProviderSnapshot(
            " cus_provider ",
            " sub_provider ",
            SubscriptionTier.Pro,
            100,
            SubscriptionStatus.Suspended,
            trialEndsAt,
            observedAt,
            appliedAt
        );

        Assert.True(applied);
        Assert.Equal("cus_provider", subscription.StripeCustomerId);
        Assert.Equal("sub_provider", subscription.StripeSubscriptionId);
        Assert.Equal(SubscriptionTier.Pro, subscription.Tier);
        Assert.Equal(100, subscription.ClientLimit);
        Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(trialEndsAt, subscription.TrialEndsAt);
        Assert.Equal(observedAt, subscription.LastProviderStateObservedAt);
        Assert.Equal(appliedAt, subscription.UpdatedAt);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    public void ApplyProviderSnapshot_NonNewerSnapshot_IsRejectedWithoutChangingState(
        int staleObservedAtMinute
    )
    {
        var subscription = Create();
        var currentObservedAt = Now.AddMinutes(5);
        var currentAppliedAt = Now.AddMinutes(6);
        subscription.ApplyProviderSnapshot(
            "cus_current",
            "sub_current",
            SubscriptionTier.Pro,
            100,
            SubscriptionStatus.Active,
            Now.AddDays(30),
            currentObservedAt,
            currentAppliedAt
        );

        var applied = subscription.ApplyProviderSnapshot(
            "cus_current",
            "sub_current",
            SubscriptionTier.Starter,
            25,
            SubscriptionStatus.Cancelled,
            null,
            Now.AddMinutes(staleObservedAtMinute),
            Now.AddMinutes(7)
        );

        Assert.False(applied);
        Assert.Equal("cus_current", subscription.StripeCustomerId);
        Assert.Equal("sub_current", subscription.StripeSubscriptionId);
        Assert.Equal(SubscriptionTier.Pro, subscription.Tier);
        Assert.Equal(100, subscription.ClientLimit);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(Now.AddDays(30), subscription.TrialEndsAt);
        Assert.Equal(currentObservedAt, subscription.LastProviderStateObservedAt);
        Assert.Equal(currentAppliedAt, subscription.UpdatedAt);
    }

    [Fact]
    public void LinkStripeSubscription_InvalidSubscriptionIdentifier_ThrowsWithoutPartialMutation()
    {
        var subscription = Create();

        Assert.Throws<DomainException>(() => subscription.LinkStripeSubscription(
            "cus_1",
            "   ",
            Now.AddMinutes(1)
        ));
        Assert.Null(subscription.StripeCustomerId);
        Assert.Null(subscription.StripeSubscriptionId);
        Assert.Equal(Now, subscription.UpdatedAt);
    }

    [Fact]
    public void ApplyProviderSnapshot_InvalidSubscriptionIdentifier_ThrowsWithoutPartialMutation()
    {
        var subscription = Create();
        subscription.LinkStripeCustomer("cus_1", Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => subscription.ApplyProviderSnapshot(
            "cus_1",
            "   ",
            SubscriptionTier.Pro,
            100,
            SubscriptionStatus.Active,
            Now.AddDays(30),
            Now.AddMinutes(2),
            Now.AddMinutes(3)
        ));
        Assert.Equal("cus_1", subscription.StripeCustomerId);
        Assert.Null(subscription.StripeSubscriptionId);
        Assert.Equal(SubscriptionTier.Free, subscription.Tier);
        Assert.Equal(5, subscription.ClientLimit);
        Assert.Null(subscription.LastProviderStateObservedAt);
        Assert.Equal(Now.AddMinutes(1), subscription.UpdatedAt);
    }

    private static TrainerSubscription Create() =>
        new(Guid.NewGuid(), Now.AddDays(15), Now);
}
