using Domain.ValueObjects;
using Domain.Exceptions;
using Xunit;

namespace Unit.Domain.UnitTests.ValueObjects;

public sealed class SubscriptionStatusTests
{
    [Theory]
    [MemberData(nameof(KnownStatusValuesData))]
    public void FromString_KnownValue_ReturnsCorrespondingSingleton(string value, SubscriptionStatus expected)
    {
        // Act
        var result = SubscriptionStatus.FromString(value);

        // Assert
        Assert.Same(expected, result);
    }

    [Fact]
    public void FromString_UnknownValue_ThrowsDomainException()
    {
        // Arrange
        var unknownValue = "unknown_status";

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => SubscriptionStatus.FromString(unknownValue));
        Assert.Equal($"Invalid subscription status: {unknownValue}", exception.Message);
    }

    public static TheoryData<string, SubscriptionStatus> KnownStatusValuesData =>
        new()
        {
            { "ACTIVE", SubscriptionStatus.Active },
            { "INACTIVE", SubscriptionStatus.Inactive },
            { "SUSPENDED", SubscriptionStatus.Suspended },
            { "CANCELLED", SubscriptionStatus.Cancelled }
        };
}
