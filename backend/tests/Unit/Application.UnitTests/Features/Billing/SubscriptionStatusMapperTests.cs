using Application.Features.Billing.Webhooks;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Billing;

public sealed class SubscriptionStatusMapperTests
{
    [Theory]
    [InlineData("trialing", "ACTIVE")]
    [InlineData("active", "ACTIVE")]
    [InlineData("incomplete", "INACTIVE")]
    [InlineData("incomplete_expired", "INACTIVE")]
    [InlineData("past_due", "SUSPENDED")]
    [InlineData("unpaid", "SUSPENDED")]
    [InlineData("paused", "SUSPENDED")]
    [InlineData("canceled", "CANCELLED")]
    public void Map_KnownProviderStatus_ReturnsLocalStatus(
        string providerStatus,
        string expected)
    {
        var status = SubscriptionStatusMapper.Map(providerStatus);

        Assert.Equal(expected, status.Value);
    }

    [Theory]
    [InlineData(" Active ")]
    [InlineData("ACTIVE")]
    public void Map_ProviderStatusWithCasingOrWhitespace_IsNormalized(
        string providerStatus)
    {
        var status = SubscriptionStatusMapper.Map(providerStatus);

        Assert.Equal(SubscriptionStatus.Active, status);
    }

    [Theory]
    [InlineData("pending_activation")]
    [InlineData("some_future_status")]
    public void Map_UnknownNonEmptyStatus_IsConservativelyInactive(
        string providerStatus)
    {
        var status = SubscriptionStatusMapper.Map(providerStatus);

        Assert.Equal(SubscriptionStatus.Inactive, status);
    }

    [Fact]
    public void Map_NullStatus_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SubscriptionStatusMapper.Map(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_EmptyOrWhitespaceStatus_IsRejected(string providerStatus)
    {
        Assert.Throws<ArgumentException>(() =>
            SubscriptionStatusMapper.Map(providerStatus));
    }
}
