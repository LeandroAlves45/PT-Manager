using Domain.ValueObjects;
using Domain.Exceptions;
using Xunit;

namespace Unit.Domain.UnitTests.ValueObjects;

public sealed class SubscriptionTierTests
{
    [Theory]
    [MemberData(nameof(KnownTierValuesData))]
    public void FromString_KnownValue_ReturnsCorrespondingSingleton(string value, SubscriptionTier expected)
    {
        // Act
        var result = SubscriptionTier.FromString(value);

        // Assert
        Assert.Same(expected, result);
    }

    [Fact]
    public void FromString_UnknownValue_ThrowsDomainException()
    {
        // Arrange
        var unknownValue = "unknown_tier";

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => SubscriptionTier.FromString(unknownValue));
        Assert.Equal($"Invalid subscription tier: {unknownValue}", exception.Message);
    }

    public static TheoryData<string, SubscriptionTier> KnownTierValuesData =>
        new()
        {
            { "FREE", SubscriptionTier.Free },
            { "STARTER", SubscriptionTier.Starter },
            { "PRO", SubscriptionTier.Pro }
        };
}
