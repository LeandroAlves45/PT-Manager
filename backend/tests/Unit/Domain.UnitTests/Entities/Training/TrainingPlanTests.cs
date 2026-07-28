using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public class TrainingPlanTests
{
    [Fact]
    public void Constructor_StartAfterEnd_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 10);
        var endsDate = new DateOnly(2026, 8, 1);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new TrainingPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Training Plan",
            null,
            null,
            null,
            startsDate,
            endsDate,
            now
        ));
        Assert.Equal("Training plan end date cannot be before start date.", exception.Message);
    }

    [Fact]
    public void Activate_DeletedPlan_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 1);
        var endsDate = new DateOnly(2026, 8, 10);

        var trainingPlan = new TrainingPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Training Plan",
            null,
            null,
            null,
            startsDate,
            endsDate,
            now
        );

        // Mark the plan as deleted
        trainingPlan.Delete(now);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => trainingPlan.Activate(now));
        Assert.Equal("Cannot activate a deleted training plan.", exception.Message);
    }
}
