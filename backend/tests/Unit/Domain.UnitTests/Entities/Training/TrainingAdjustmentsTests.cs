using Domain.Entities.Training;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public sealed class TrainingAdjustmentsTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Exercise_SetActive_ChangesAvailability()
    {
        // Arrange
        var exercise = new Exercise(
            Guid.NewGuid(),
            "Squat",
            null,
            "Legs",
            null,
            null,
            null,
            Now
        );

        // Act
        exercise.SetActive(false, Now.AddMinutes(1));

        // Assert
        Assert.False(exercise.IsActive);
    }

    [Fact]
    public void ClientExerciseSetLog_Correct_ReplacesLastPerfomance()
    {
        // Arrange
        var log = new ClientExerciseSetLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            50,
            8,
            null,
            Now
        );

        // Act
        log.Correct(55, 9, null, Now.AddDays(7));

        // Assert
        Assert.Equal((55m, 9), (log.WeightKg, log.RepsDone));
    }
}
