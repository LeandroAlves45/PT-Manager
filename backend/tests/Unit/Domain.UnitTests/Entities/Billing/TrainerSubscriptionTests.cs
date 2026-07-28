using Domain.Entities.Billing;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Billing;

public sealed class TrainerSubscriptionTests
{
    [Fact]
    public void CanAddClient_AtLimit_ReturnsFalse()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var subscriptionTier = SubscriptionTier.FromString("FREE");
        var trialEndDate = now.AddDays(7);
        var subscription = new TrainerSubscription(
            Guid.NewGuid(),
            trialEndDate,
            now
        );

        // Act
        subscription.ChangeTier(subscriptionTier, 5, now);
        for (var i = 0; i < 5; i++)
            subscription.RegisterClientAdded(now);

        // Assert
        Assert.False(subscription.CanAddClient());
    }

    [Fact]
    public void CanAddClient_ExemptFromBilling_IgnoresLimit()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var isExemptFromBilling = true;
        var trialEndDate = now.AddDays(7);
        var subscription = new TrainerSubscription(
            Guid.NewGuid(),
            trialEndDate,
            now
        );

        // Act
        subscription.SetBillingExemption(isExemptFromBilling, now);
        for (var i = 0; i < 6; i++) // ultrapassa o ClientLimit (5) para provar que a isenção ignora o limite
            subscription.RegisterClientAdded(now);

        // Assert
        Assert.True(subscription.CanAddClient());
    }

    [Fact]
    public void RegisterClientAdded_AtLimit_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var subscriptionTier = SubscriptionTier.FromString("FREE");
        var trialEndDate = now.AddDays(7);
        var subscription = new TrainerSubscription(
            Guid.NewGuid(),
            trialEndDate,
            now
        );

        // Act
        subscription.ChangeTier(subscriptionTier, 5, now);
        for (var i = 0; i < 5; i++)
            subscription.RegisterClientAdded(now);

        // Assert
        var exception = Assert.Throws<DomainException>(() => subscription.RegisterClientAdded(now));
        Assert.Equal("Client limit reached for the current subscription tier.", exception.Message);
    }
}
