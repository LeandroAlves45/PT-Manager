using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public class ClientExerciseSetLogTests
{
    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(16, 3, 3)]
    [InlineData(10, -1, 10)]
    [InlineData(10, 10, -1)]
    [InlineData(10, 10, 101)]
    public void Constructor_OutOfRangeValues_ThrowsDomainException(
        int setNumber, int weightKg, int repsDone)
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var performedAt = new DateTimeOffset(now);
        // Act & Assert
        Assert.Throws<DomainException>(() => new ClientExerciseSetLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            setNumber,
            weightKg,
            repsDone,
            null,
            performedAt,
            now
        ));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    [InlineData(10, 101)]
    public void Correct_OutOfRangeValues_ThrowsDomainException(
        int weightKg,
        int repsDone
    )
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var performedAt = new DateTimeOffset(now);
        // Act & Assert
        var clientExerciseSetLog = new ClientExerciseSetLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            10,
            10,
            null,
            performedAt,
            now
        );
        Assert.Throws<DomainException>(() => clientExerciseSetLog.Correct(
            weightKg,
            repsDone,
            null,
            performedAt,
            now
        ));
    }
}
