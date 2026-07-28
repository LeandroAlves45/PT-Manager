using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public sealed class ExerciseSetTests
{
    [Theory]
    [InlineData(-1, null, null)]
    [InlineData(null, -1, null)]
    [InlineData(null, null, -1)]
    public void Constructor_NegativeLoadOrRest_ThrowsDomainException(
        int? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax
    )
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => new ExerciseSet(
            Guid.NewGuid(),
            1,
            10,
            plannedWeightKg,
            restSecondsMin,
            restSecondsMax,
            new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc)
        ));
    }
}
