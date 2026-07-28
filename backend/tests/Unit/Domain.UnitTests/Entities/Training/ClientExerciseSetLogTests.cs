using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public class ClientExerciseSetLogTests
{
    [Theory]
    [InlineData(0, 10, 10, "Set number must be between 1 and 15.")]
    [InlineData(16, 3, 3, "Set number must be between 1 and 15.")]
    [InlineData(10, -1, 10, "Weight cannot be negative.")]
    [InlineData(10, 10, -1, "Reps done must be between 0 and 100.")]
    [InlineData(10, 10, 101, "Reps done must be between 0 and 100.")]
    public void Constructor_OutOfRangeValues_ThrowsDomainException(
        int setNumber, int weightKg, int repsDone, string expectedMessage)
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new ClientExerciseSetLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            setNumber,
            weightKg,
            repsDone,
            null,
            now,
            now
        ));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(-1, 10, "Weight cannot be negative.")]
    [InlineData(10, -1, "Reps done must be between 0 and 100.")]
    [InlineData(10, 101, "Reps done must be between 0 and 100.")]
    public void Correct_OutOfRangeValues_ThrowsDomainException(
        int weightKg,
        int repsDone,
        string expectedMessage
    )
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        // Act & Assert
        var clientExerciseSetLog = new ClientExerciseSetLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            10,
            10,
            null,
            now,
            now
        );
        var exception1 = Assert.Throws<DomainException>(() => clientExerciseSetLog.Correct(
            weightKg,
            repsDone,
            null,
            now
        ));
        Assert.Equal(expectedMessage, exception1.Message);
    }
}
