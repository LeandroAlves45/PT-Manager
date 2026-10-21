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

    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_RepsRange_KeepsMinimumAndMaximum()
    {
        var set = new ExerciseSet(Guid.NewGuid(), 1, 10, null, null, null, Now, plannedRepsMax: 12);

        Assert.Equal((10, 12), (set.PlannedReps, set.PlannedRepsMax));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(12, 10)]
    [InlineData(null, 12)]
    public void Constructor_InvalidRepsRange_ThrowsDomainException(int? plannedReps, int? plannedRepsMax)
    {
        Assert.Throws<DomainException>(() => new ExerciseSet(
            Guid.NewGuid(), 1, plannedReps, null, null, null, Now, plannedRepsMax: plannedRepsMax));
    }

    [Fact]
    public void UpdateSet_ClearsMaximum_WhenSentNull()
    {
        var prescription = new TrainingPlanDayExercise(Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, Now);
        var set = prescription.AddSet(1, 10, null, null, null, Now, plannedRepsMax: 12);

        prescription.UpdateSet(set.Id, 1, 8, null, null, null, null, null, Now.AddMinutes(1));

        Assert.Equal((8, (int?)null), (set.PlannedReps, set.PlannedRepsMax));
    }
}
