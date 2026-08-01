using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public sealed class BodyMeasurementsTests
{
    [Fact]
    public void Constructor_AllValuesNull_CreatesEmptyEquivalent()
    {
        // Act
        var bodyMeasurements = new BodyMeasurements(null, null, null, null, null, null, null, null, null);

        // Assert
        Assert.Equal(BodyMeasurements.Empty, bodyMeasurements);
    }

    [Theory]
    [InlineData(60.5)]
    [InlineData(0.1)]
    public void Constructor_PositiveWaist_Accepted(decimal waistCm)
    {
        // Act
        var bodyMeasurements = new BodyMeasurements(waistCm, null, null, null, null, null, null, null, null);

        // Assert
        Assert.Equal(waistCm, bodyMeasurements.WaistCm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveWaist_ThrowsException(decimal waistCm)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            new BodyMeasurements(waistCm, null, null, null, null, null, null, null, null));
    }

    [Theory]
    [MemberData(nameof(GetNonPositiveMeasurement))]
    public void Constructor_NonPositiveMeasurement_Throws(string field)
    {
        Assert.Throws<DomainException>(() =>
        {
            _ = field switch
            {
                "waist" => new BodyMeasurements(0, null, null, null, null, null, null, null, null),
                "hip" => new BodyMeasurements(null, 0, null, null, null, null, null, null, null),
                "chest" => new BodyMeasurements(null, null, 0, null, null, null, null, null, null),
                "right_arm" => new BodyMeasurements(null, null, null, 0, null, null, null, null, null),
                "left_arm" => new BodyMeasurements(null, null, null, null, 0, null, null, null, null),
                "right_thigh" => new BodyMeasurements(null, null, null, null, null, 0, null, null, null),
                "left_thigh" => new BodyMeasurements(null, null, null, null, null, null, 0, null, null),
                "right_calf" => new BodyMeasurements(null, null, null, null, null, null, null, 0, null),
                "left_calf" => new BodyMeasurements(null, null, null, null, null, null, null, null, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            };
        });
    }
    public static IEnumerable<object[]> GetNonPositiveMeasurement()
    {
        yield return new object[] { "waist" };
        yield return new object[] { "hip" };
        yield return new object[] { "chest" };
        yield return new object[] { "right_arm" };
        yield return new object[] { "left_arm" };
        yield return new object[] { "right_thigh" };
        yield return new object[] { "left_thigh" };
        yield return new object[] { "right_calf" };
        yield return new object[] { "left_calf" };
    }
}
