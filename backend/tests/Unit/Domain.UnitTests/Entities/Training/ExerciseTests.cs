using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public sealed class ExerciseTests
{
    [Fact]
    public void Update_DeletedExercise_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var exercise = new Exercise(null, "Squat", null, null, null, null, null, now);
        exercise.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            exercise.Update("Front squat", null, null, null, null, null, now.AddMinutes(1)));
    }
}
